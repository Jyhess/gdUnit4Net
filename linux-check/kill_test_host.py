"""Runs the blocking test, kills its test host, and reaps the orphaned Godot runtime to report its exit code and delay.

This process is a subreaper: the Godot runtime whose test host is killed is given to it, as to a subreaper on a CI runner.
"""

import ctypes
import os
import signal
import subprocess
import sys
import time

PR_SET_CHILD_SUBREAPER = 36
SCENE = "GdUnit4TestRunnerScene"


def processes():
    """Yields (pid, parent pid, command line) of every process."""
    for entry in os.listdir("/proc"):
        if not entry.isdigit():
            continue
        try:
            with open(f"/proc/{entry}/stat") as stat:
                fields = stat.read().rsplit(")", 1)[1].split()
            with open(f"/proc/{entry}/cmdline", "rb") as cmdline:
                command = cmdline.read().replace(b"\0", b" ").decode(errors="replace")
        except OSError:
            continue
        yield int(entry), int(fields[1]), command


def main():
    ctypes.CDLL("libc.so.6", use_errno=True).prctl(PR_SET_CHILD_SUBREAPER, 1, 0, 0, 0)
    run = subprocess.Popen(
        ["dotnet", "test", "Api.Test", "--settings", "Api.Test/linux.runsettings",
         "--filter", "FullyQualifiedName~LinuxCheck.BlockingTest"])
    deadline = time.monotonic() + 600
    godot = None
    while godot is None and time.monotonic() < deadline:
        time.sleep(0.5)
        godot = next(((pid, parent) for pid, parent, command in processes()
                      if SCENE in command and "dotnet" not in command.split(" ")[0]), None)
    if godot is None:
        print("FAIL: no Godot runtime started")
        return 1
    time.sleep(5)
    godot_pid, test_host_pid = godot
    with open(f"/proc/{test_host_pid}/cmdline", "rb") as cmdline:
        print("Test host:", test_host_pid, cmdline.read().replace(b"\0", b" ").decode()[:120])
    print("Godot runtime:", godot_pid)
    killed_at = time.monotonic()
    os.kill(test_host_pid, signal.SIGKILL)
    while time.monotonic() - killed_at < 10:
        pid, status = os.waitpid(-1, os.WNOHANG)
        if pid == godot_pid:
            delay = time.monotonic() - killed_at
            code = os.waitstatus_to_exitcode(status)
            print(f"Godot runtime ended after {delay:.2f} s with code {code}")
            run.wait()
            return 0 if code == 101 else 1
        time.sleep(0.05)
    print("FAIL: the Godot runtime still runs 10 s after its test host was killed")
    os.kill(godot_pid, signal.SIGKILL)
    return 1


if __name__ == "__main__":
    sys.exit(main())
