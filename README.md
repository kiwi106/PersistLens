# PersistLens

PersistLens is a local-first, defensive Windows console tool for inventorying selected persistence mechanisms, saving JSON snapshots, and comparing them. It is not an antivirus: an unfamiliar entry, an unsigned file, or a caution indicator is not proof of malware.

## MVP

The MVP inventories Registry Run/RunOnce locations (both views), automatic services, Task Scheduler 2.0 tasks, and user/common Startup folders. It records partial collection errors, parses commands without executing them, enriches resolved local targets with streaming SHA-256 and limited certificate evidence, and supports inventory, snapshots, diffs, inspection, terminal output, and JSON output.

### Implemented in this MVP

- Read-only Registry Run/RunOnce, automatic-service configuration, Task Scheduler 2.0 COM enumeration, and Startup-folder collection.
- Full SHA-256 computed through streaming for eligible resolved local files.
- Local JSON snapshots, deterministic entry/change ordering, and structured partial errors.

### Partial capabilities

- Certificate evidence only detects a readable embedded signing certificate. It does **not** perform authoritative Windows Authenticode trust-chain validation.
- `.lnk` files are inventoried but their targets, arguments, and working directories are **not** resolved.
- File ownership is not collected.
- Service current state and scheduled-task runtime state are reported as unavailable/unknown. Task Scheduler 2.0 is used only to read definitions; no task is run or changed.
- Files above 512 MiB are not hashed; ambiguous commands remain unresolved.

### Outside the MVP

Real-time monitoring, remediation/blocking, cloud lookups, telemetry, reputation services, and malware verdicts are not implemented.

It does not monitor in real time, block/remove persistence, use cloud reputation, upload data, or make malware verdicts.

## Prerequisites and build

Requires the installed .NET 8 SDK on Windows. `dotnet restore`, `dotnet build --configuration Release`, and `dotnet test --configuration Release --no-build` build and test the solution. Run the CLI with `dotnet run --project src/PersistLens.Cli -- inventory`.

## Usage

```
persistlens inventory
persistlens inventory --format json
persistlens snapshot create --name clean
persistlens snapshot list
persistlens snapshot show clean
persistlens snapshot delete clean
persistlens diff clean current --format json
persistlens inspect <entry-id>
```

Snapshots default to `%LOCALAPPDATA%\PersistLens\snapshots`; pass `--storage <directory>` to isolate tests or choose another local directory. Snapshot names accept only ASCII letters, digits, `_`, and `-`, preventing traversal. Exit codes are 0 success, 1 differences found, 2 invalid input, 3 operational failure, and 4 significant partial collection errors. Code 4 means usable entries were returned but at least one collector reported a structured error; `snapshot create` still writes and can later read that partial snapshot. Terminal output prints every warning and JSON includes them in `result.errors` or snapshot metadata.

## Privacy and security

PersistLens makes no network calls, has no account or telemetry, and never executes discovered commands. JSON snapshots and reports may contain command lines and paths, which can contain sensitive information; treat them as sensitive. Some service/task information may be unavailable without elevation.

See [architecture](docs/architecture.md), [limitations](docs/limitations.md), [security model](docs/security-model.md), [threat model](docs/threat-model.md), and [roadmap](docs/roadmap.md). Contributions follow [CONTRIBUTING.md](CONTRIBUTING.md); the project uses the [MIT License](LICENSE).
