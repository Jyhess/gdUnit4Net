# Linux check of the test host watch and the pipe read, outside the pull requests.
# Build from the repository root: docker build -f linux-check/Dockerfile -t gdunit4-linux-check .
FROM mcr.microsoft.com/dotnet/sdk:10.0

ARG GODOT_VERSION=4.7.2
RUN apt-get update \
    && apt-get install -y --no-install-recommends unzip python3 procps libfontconfig1 \
    && rm -rf /var/lib/apt/lists/*
RUN curl -fsSL -o /tmp/godot.zip \
        "https://github.com/godotengine/godot/releases/download/${GODOT_VERSION}-stable/Godot_v${GODOT_VERSION}-stable_mono_linux_x86_64.zip" \
    && unzip -q /tmp/godot.zip -d /opt \
    && mv "/opt/Godot_v${GODOT_VERSION}-stable_mono_linux_x86_64" /opt/godot \
    && ln -s "/opt/godot/Godot_v${GODOT_VERSION}-stable_mono_linux.x86_64" /opt/godot/godot \
    && rm /tmp/godot.zip

WORKDIR /src
COPY . .
RUN rm -rf Api.Test/.godot Api.Test/TestResults */bin */obj */nupkg
CMD ["sh", "linux-check/check.sh"]
