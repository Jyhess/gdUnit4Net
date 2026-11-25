#!/bin/sh
# Runs the watch and pipe tests, then kills the test host of a blocked test and reports how the Godot runtime ended.
set -eu
cd /src
cat > /tmp/nuget.config <<'EOF'
<?xml version="1.0" encoding="utf-8"?>
<configuration><packageSources><clear /><add key="nuget.org" value="https://api.nuget.org/v3/index.json" /></packageSources></configuration>
EOF
cp /tmp/nuget.config nuget.config
sed 's#<GODOT_BIN>.*</GODOT_BIN>#<GODOT_BIN>/opt/godot/godot</GODOT_BIN>#; s#<Parameters>.*</Parameters>#<Parameters>--headless --verbose</Parameters>#' \
    Api.Test/.runsettings > Api.Test/linux.runsettings

echo "== Api.Test: TestHostWatchTest and InOutPipeProxyTest"
dotnet test Api.Test --settings Api.Test/linux.runsettings \
    --filter "FullyQualifiedName~TestHostWatchTest|FullyQualifiedName~InOutPipeProxyTest"

echo "== Killing the test host of a blocked test"
mkdir -p Api.Test/src/linuxcheck
cat > Api.Test/src/linuxcheck/BlockingTest.cs <<'EOF'
namespace GdUnit4.Tests.LinuxCheck;

using System.Threading;

[TestSuite]
public class BlockingTest
{
    [TestCase]
    [RequireGodotRuntime]
    public void Blocks() => Thread.Sleep(Timeout.Infinite);
}
EOF
python3 linux-check/kill_test_host.py
