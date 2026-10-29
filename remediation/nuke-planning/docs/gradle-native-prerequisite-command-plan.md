# Native Gradle delegate prerequisite and invocation

Prepared commands only. Root controls execution. The emitter and eleven actual NUKE alias cases have passed; Java on this host and actual Gradle delegation have not been admitted yet. No downloads, global install, Bash, Node or old Gradle producer are required by this plan.

Use one fresh ignored run root. The existing image digest pins the JDK's source; `release` and the entire `legal` directory must remain in the private copy. A successful Docker copy is not proof that its JDK can run on this host. Stop on missing host dependencies, version mismatch, unknown image identity or unsafe copied links; retain the failed cache and diagnostics.

```powershell
$workspace='/home/sharpninja/github/2026-10-26/task-3'
$app="$workspace/remediation/nuke-app"
$design="$workspace/remediation/design-c4"
$dotnet='/home/sharpninja/.dotnet/dotnet'
$docker='/usr/bin/docker'
$image='structurizr/cli@sha256:0399ac8e24c16e41277cfcec22caa29e4775013e5395c0720016828426d62749'
$run="$app/artifacts/gradle-delegate-native-001"
$jdk="$run/tool-cache/jdk"
$gradle="$design/.tools/gradle-8.14.3"
$launcher="$gradle/lib/gradle-launcher-8.14.3.jar"
```

Before effects, verify the existing Docker identity (`70f68a7702346cf4bb1a2568091cfee92216cc8498a627a488c4b212bc5f45c9`), native dotnet identity (`634efbf2414144d140d6f59d167342c8b3b53b4c34346ed786dd0470ddc9a3cb`), current toolchain lock (`94dbb4754728e970ec1ff37f358f987b76e5eb1e8a456a2b14e003bfc218751e`), Gradle ZIP SHA/size from that lock, and the actual emitter manifest (`eb7c3cbb87dd6e2c739a6917f7399fab3b3d6f61a5dab50683cdb1f3052acc58`). The current extracted launcher SHA is `f59f13bf282e831e726da3dcf17e31f9c4bb9410ad0671136dd2aae7a890dd57`, length 327155; Gradle LICENSE SHA is `9536d88ea948603d18e232a13f5958d67807cd80828036b082bff171d2cf0703`. Reuse the existing proven ZIP admission, including member/size/hash checks, rather than claiming the launcher hash alone establishes the whole distribution.

Create only `$run` and its children after checking absence. Write `{}` as UTF-8 to `$run/.docker-cli/config.json`. All Docker requests use the explicit local socket/config and the pinned existing image. The limited JAVA_HOME template emits only that one environment field, never the environment collection.

```powershell
$dockerPrefix=@('--host','unix:///var/run/docker.sock','--config',"$run/.docker-cli")
& $docker @dockerPrefix image inspect --format '{{json .RepoDigests}}' $image
& $docker @dockerPrefix image inspect --format '{{.Os}} {{.Architecture}} {{.Id}}' $image
$javaHomeLine=& $docker @dockerPrefix image inspect --format '{{range .Config.Env}}{{if eq (index (split . "=") 0) "JAVA_HOME"}}{{println .}}{{end}}{{end}}' $image
```

Require exact requested RepoDigest membership, Linux/host architecture compatibility, one `JAVA_HOME=` line and a confined absolute path with no `.`/`..` segments or control characters. Do not guess an alternate path. Retain these observations. If admitted, create a new uniquely named container, do not start it, copy only its observed JDK directory into the private ignored cache, then remove only the exact returned container ID. Validate the returned ID as 64 hexadecimal characters and retain it before any cleanup.

```powershell
$javaHome=$javaHomeLine.Substring('JAVA_HOME='.Length)
$containerName='android-mcp-gradle-jdk-'+[Guid]::NewGuid().ToString('N')
$containerId=& $docker @dockerPrefix create --pull=never --network none --name $containerName $image
& $docker @dockerPrefix cp "${containerId}:$javaHome/." $jdk
# Record copied regular-file hashes and literal relative-link targets without following links.
# Require release, bin/java, lib/modules, and legal; retain the full legal tree.
& $docker @dockerPrefix rm $containerId
```

Before Java execution, admit the copied `bin/java` and all its ancestors through existing path policy, record its SHA/size and the release/license provenance. Resolve no external symlink. Run its version check through the existing process/receipt seam, with retained stdout/stderr and no changed PATH/JAVA_HOME/settings:

```powershell
$java="$jdk/bin/java"
& $java -version
```

Require successful exit and the pinned `21.0.8` version. This is the first actual host compatibility observation. Missing ELF loader/native library is a prerequisite failure, not permission to install or alter host libraries. Java inside the pinned image remains a fallback candidate only: container Gradle delegation also needs observed compatibility for the mounted existing dotnet SDK/runtime/package cache; that compatibility has not been proven.

For the first actual Gradle check, use a separate `$run/project` with byte-exact copies of the two generated artifacts and current canonical `architecture/code-diagrams.md`/`architecture/workspace.dsl`. Preserve the emitter generation root unchanged. Record these four input identities before and after. The NUKE operation's narrow source admission is already proven by eleven native cases. Use current-source expected PUML identities from the reviewed native render generation for the positive current-input command, rather than historical server-core PUML bytes.

```powershell
$gradleArgs=@('-cp',$launcher,'org.gradle.launcher.GradleMain',
  '--offline','--no-daemon','--console=plain','--gradle-user-home',"$run/gradle-user-home",
  '--project-dir',"$run/project")
# Clean tasks listing needs no NUKE project build and executes no alias.
& $java @gradleArgs tasks --all

$delegateArgs=@('extractCodeViews',
  "-PnukeDotnetHost=$dotnet","-PnukeProject=$app/AndroidMcp.Build.csproj",
  "-PnukeProjectRoot=$run/project","-PnukeOutputRoot=$run/final",
  "-PnukeCacheRoot=$run/unused-cache","-PnukeWorkRoot=$run/unused-work",
  "-PnukeReceiptRoot=$run/unused-tool-receipts","-PnukeDockerIdentity=$run/absent-docker.json",
  '-PnukeRendererUser=1000:1000','-PnukeToolAccess=Online',
  '-PnukeGeneration=gradle-delegate-native-001','-PnukePolicyRelativePath=../must-not-be-read')
& $java @gradleArgs @delegateArgs
```

Both commands must retain process receipts. `--offline` governs Gradle dependency access; the `Online` typed value is a required NUKE parameter and must never be dereferenced by the narrow CodeExtract selection. Verify one `nukeDesignCompatibility` Exec dispatch, actual NUKE generation manifest with exactly four inputs and six editable outputs, byte-exact current expected PUML, unchanged four-source pins, and absent unused cache/work/tool-receipt/Docker paths. The generated delegate intentionally executes the existing dotnet project (`run --no-restore`), so Root must reserve the source/build window for the actual Gradle call. This differs from the already-proven test probe's build-free `dotnet exec` route.

The thirteen-alias metadata and private stage graph remain Core's sole authority. A single narrow actual invocation does not establish runtime parity for every alias or finish the five receipt/source-package duties.
