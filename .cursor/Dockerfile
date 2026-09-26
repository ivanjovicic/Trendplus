# Cloud Agent base image for Trendplus.
# Provides the full local development toolchain:
#   - .NET 8 SDK (backend API + workers)
#   - Node.js 22 (React/Vite frontend)
#   - PostgreSQL 16 + pgvector (primary + analytics databases)
#   - mdbtools/unixODBC (legacy MS Access import support used by AccessImportService)
FROM ubuntu:24.04

ENV DEBIAN_FRONTEND=noninteractive \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    DOTNET_ROOT=/usr/lib/dotnet \
    PATH=/usr/lib/dotnet:/usr/lib/dotnet/tools:/usr/local/bin:/usr/local/sbin:/usr/bin:/usr/sbin:/bin:/sbin

# Base OS packages + PostgreSQL 16 (+ pgvector) + Access import tooling.
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        ca-certificates curl git gnupg sudo tzdata locales \
        build-essential \
        postgresql-16 postgresql-16-pgvector postgresql-client-16 \
        unixodbc libodbc2 mdbtools odbc-mdbtools \
    && sed -i 's/^# *\(en_US.UTF-8\)/\1/' /etc/locale.gen && locale-gen \
    && rm -rf /var/lib/apt/lists/*

ENV LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8

# Node.js 22 (NodeSource).
RUN curl -fsSL https://deb.nodesource.com/setup_22.x | bash - \
    && apt-get install -y --no-install-recommends nodejs \
    && rm -rf /var/lib/apt/lists/*

# .NET 8 SDK via the official dotnet-install script (system-wide).
RUN curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \
    && bash /tmp/dotnet-install.sh --channel 8.0 --install-dir /usr/lib/dotnet \
    && rm -f /tmp/dotnet-install.sh \
    && ln -sf /usr/lib/dotnet/dotnet /usr/local/bin/dotnet \
    && dotnet --info

# Configure the MDBTools ODBC driver so AccessImportService can read .mdb/.accdb.
RUN MDBLIB=$(find /usr -name "libmdbodbc*.so*" -print -quit 2>/dev/null) \
    && if [ -n "$MDBLIB" ]; then \
         printf "[MDBTools]\nDescription=MDBTools ODBC Driver\nDriver=%s\nSetup=\nFileUsage=1\n" "$MDBLIB" > /etc/odbcinst.ini; \
       fi

# Let the (pre-existing) ubuntu user manage the local PostgreSQL cluster and use sudo.
RUN echo 'ubuntu ALL=(ALL) NOPASSWD:ALL' > /etc/sudoers.d/90-ubuntu \
    && chmod 0440 /etc/sudoers.d/90-ubuntu \
    && usermod -aG postgres ubuntu

USER ubuntu
WORKDIR /workspace
