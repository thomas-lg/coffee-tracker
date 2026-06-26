# Dev container image for Coffee Tracker.
# Base: Microsoft's .NET 10 dev container image — UBUNTU 24.04 "Noble"
# (.NET 10 moved off Debian; there is no 1-10.0-bookworm/trixie release tag).
# Ships the .NET 10 SDK + the `vscode` non-root user. Node + Angular CLI are
# layered via the devcontainer Node feature + post-create.sh; this Dockerfile
# only adds the native Tesseract OCR libraries that features can't.
# Fallback tag if 1-10.0 ever fails: 1-10.0-noble.
FROM mcr.microsoft.com/devcontainers/dotnet:1-10.0

# The base image ships a yarn apt source whose GPG signing key has rotated,
# so `apt-get update` hard-fails on dl.yarnpkg.com ("NO_PUBKEY ... repository
# is not signed"). We don't use that repo — Node/yarn come from the Node
# devcontainer feature — so remove the source before updating.
#
# Native OCR deps (M5). Keep the -dev packages: the TesseractOCR NuGet P/Invokes
# the UNVERSIONED libtesseract.so / liblept.so soname, which -dev provides.
# tesseract-ocr-eng bundles eng.traineddata.
RUN rm -f /etc/apt/sources.list.d/yarn.list \
    && apt-get update \
    && export DEBIAN_FRONTEND=noninteractive \
    && apt-get install -y --no-install-recommends \
        tesseract-ocr tesseract-ocr-eng libtesseract-dev libleptonica-dev \
    && apt-get clean && rm -rf /var/lib/apt/lists/*

# TESSDATA_PREFIX must be the PARENT of the tessdata dir (Tesseract appends
# /tessdata). On Ubuntu Noble eng.traineddata is at /usr/share/tesseract-ocr/5/tessdata.
ENV TESSDATA_PREFIX=/usr/share/tesseract-ocr/5
