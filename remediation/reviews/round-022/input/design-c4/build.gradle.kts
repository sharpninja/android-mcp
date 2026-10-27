import java.security.MessageDigest
import java.util.Base64

plugins { base }

val toolDir = layout.projectDirectory.dir(".tools")
val diagrams = layout.projectDirectory.dir("docs/diagrams")
val architecture = layout.projectDirectory.dir("architecture")
val pumlJar = toolDir.file("plantuml-1.2025.10.jar")
val cli = providers.environmentVariable("STRUCTURIZR_CLI").orElse("/usr/local/structurizr-cli/structurizr.sh")
val expectedNativeViews = listOf("Context", "Containers", "ServerComponents", "ServerCoreComponents", "ExecutionComponents", "ArcComponents", "Startup", "InvocationFlow", "CallbackFlow", "GroupFlow", "ArcSession", "OwnerTransition", "Deployment")
val expectedCodeViews = listOf("server-core-classes", "server-execution-classes", "arc-ui-classes", "arc-controller-classes", "arc-target-classes")
val expectedUiViews = (1..11).map { "SCR%02d".format(it) } + (1..6).map { "STB%02d".format(it) }

fun sha256(file: File): String {
    val digest = MessageDigest.getInstance("SHA-256")
    file.inputStream().use { input ->
        val buffer = ByteArray(65536)
        while (true) { val count = input.read(buffer); if (count < 0) break; digest.update(buffer, 0, count) }
    }
    return digest.digest().joinToString("") { "%02x".format(it) }
}
fun javaCommand(): String = File(System.getProperty("java.home"), "bin/java").absolutePath
fun copyVectorWithCanvas(source: File, target: File) {
    val svg = source.readText().replace(Regex("<!--SRC=\\[.*?\\]-->", RegexOption.DOT_MATCHES_ALL), "")
    val end = svg.indexOf('>', svg.indexOf("<svg")) + 1
    check(end > 0) { "Invalid generated SVG root: $source" }
    target.writeText(svg.substring(0, end) + "<rect data-canvas=\"white\" x=\"0\" y=\"0\" width=\"100%\" height=\"100%\" fill=\"#ffffff\"/>" + svg.substring(end))
}

val verifyToolchain by tasks.registering {
    inputs.files("toolchain-lock.json", "renderer-lock.json", "tooling/marp-package-lock.json", pumlJar)
    doLast {
        check(pumlJar.asFile.isFile) { "Pinned PlantUML missing: bootstrap task-local dependencies first" }
        val lock = file("toolchain-lock.json").readText()
        check(lock.contains(sha256(pumlJar.asFile))) { "PlantUML checksum differs from recorded toolchain" }
        val pins = Regex("\"file\":\\s*\"([^\"]+)\".*?\"sha256\":\\s*\"([a-f0-9]{64})\"", RegexOption.DOT_MATCHES_ALL).findAll(lock).toList()
        check(pins.size == 5) { "Build-tool checksum manifest incomplete" }
        pins.forEach { pin -> check(sha256(file(pin.groupValues[1])) == pin.groupValues[2]) { "Build-tool checksum mismatch: ${pin.groupValues[1]}" } }
        check(file(cli.get()).isFile) { "Use the documented digest-pinned local Structurizr container" }
        val renderer = file("renderer-lock.json").readText()
        fun pin(name: String): String = Regex("\"$name\"\\s*:\\s*\"([^\"]+)\"").find(renderer)?.groupValues?.get(1) ?: error("Missing renderer pin: $name")
        check(System.getProperty("java.version") == pin("java_version")) { "JDK differs from pinned renderer profile; use documented existing image" }
        val rendererPins = Regex("\"path\"\\s*:\\s*\"([^\"]+)\".*?\"sha256\"\\s*:\\s*\"([a-f0-9]{64})\"", RegexOption.DOT_MATCHES_ALL).findAll(renderer).toList()
        check(rendererPins.size == 5) { "Renderer identity lock incomplete" }
        rendererPins.forEach { value ->
            val selected = file(cli.get()).parentFile.resolve(value.groupValues[1])
            check(selected.isFile && sha256(selected) == value.groupValues[2]) { "Structurizr renderer identity mismatch: $selected" }
        }
        check(sha256(file("tooling/marp-package-lock.json")) == pin("marp_lock_sha256")) { "Committed Marp lock differs from pin" }
        val cachedLock = file(".tools/marp/package-lock.json")
        val cachedPackage = file(".tools/marp/node_modules/@marp-team/marp-cli/package.json")
        check(cachedLock.isFile && cachedPackage.isFile) { "Offline Marp cache missing; run documented task-local bootstrap before offline build" }
        check(sha256(cachedLock) == pin("marp_lock_sha256")) { "Cached Marp lock mismatch; bootstrap exact committed lock" }
        check(Regex("\"version\"\\s*:\\s*\"${Regex.escape(pin("marp_version"))}\"").containsMatchIn(cachedPackage.readText())) { "Cached Marp CLI version mismatch" }
        val node = providers.environmentVariable("NODE_BIN").orElse("node").get()
        val version = ProcessBuilder(node, "--version").redirectErrorStream(true).start()
        val versionText = version.inputStream.bufferedReader().readText().trim()
        check(version.waitFor() == 0 && versionText == pin("node_version")) { "Node runtime mismatch/missing: expected ${pin("node_version")}, received $versionText" }
    }
}

val extractCodeViews by tasks.registering {
    inputs.file("architecture/code-diagrams.md")
    outputs.dir(layout.buildDirectory.dir("code-views"))
    doLast {
        val source = file("architecture/code-diagrams.md").readText()
        val blocks = Regex("## ([a-z-]+)[^\\n]*\\n.*?```plantuml\\n(.*?)\\n```", RegexOption.DOT_MATCHES_ALL).findAll(source).associate { it.groupValues[1] to it.groupValues[2] }
        check(blocks.keys == expectedCodeViews.toSet()) { "Missing/unexpected editable code view: ${blocks.keys}" }
        val out = layout.buildDirectory.dir("code-views").get().asFile.apply { mkdirs() }
        blocks.forEach { (key, body) -> File(out, "$key.puml").writeText(body + "\n") }
    }
}

val renderCodeViews by tasks.registering {
    dependsOn(verifyToolchain, extractCodeViews)
    inputs.files(fileTree("build/code-views") { include("*.puml") }, pumlJar, "build.gradle.kts", "renderer-lock.json")
    outputs.files(expectedCodeViews.map { diagrams.file("$it.svg") })
    doLast {
        diagrams.asFile.mkdirs()
        val sources = fileTree("build/code-views") { include("*.puml") }.files.sorted()
        val process = ProcessBuilder(listOf(javaCommand(), "-Djava.awt.headless=true", "-jar", pumlJar.asFile.absolutePath, "-tsvg", "-nometadata", "-charset", "UTF-8") + sources.map { it.absolutePath }).inheritIO().start()
        check(process.waitFor() == 0) { "Code-view rendering failed" }
        expectedCodeViews.forEach { copyVectorWithCanvas(file("build/code-views/$it.svg"), diagrams.file("$it.svg").asFile) }
    }
}

val validateArchitecture by tasks.registering(Exec::class) {
    dependsOn(renderCodeViews)
    inputs.files("architecture/workspace.dsl", "architecture/code-diagrams.md", "toolchain-lock.json")
    inputs.files(expectedCodeViews.map { diagrams.file("$it.svg") })
    commandLine(cli.get(), "validate", "-workspace", architecture.file("workspace.dsl").asFile.absolutePath)
    doLast {
        layout.buildDirectory.file("receipts/architecture-validation.txt").get().asFile.apply { parentFile.mkdirs(); writeText("Structurizr validation exited0\nworkspace_sha256=${sha256(file("architecture/workspace.dsl"))}\n") }
    }
}

val exportArchitecture by tasks.registering(Exec::class) {
    dependsOn(validateArchitecture)
    inputs.files("architecture/workspace.dsl", "architecture/render-skin.puml", "build.gradle.kts")
    outputs.dir(layout.buildDirectory.dir("structurizr-export"))
    commandLine(cli.get(), "export", "-workspace", architecture.file("workspace.dsl").asFile.absolutePath, "-format", "plantuml", "-output", layout.buildDirectory.dir("structurizr-export").get().asFile.absolutePath)
    doLast {
        // Pinned exporter does not emit the global font property. Apply only this
        // local rendering profile after model export; never rewrite model semantics.
        val include = "!include ../../architecture/render-skin.puml"
        fileTree("build/structurizr-export") { include("*.puml") }.files.forEach { source ->
            val body = source.readText()
            if (!body.contains(include)) source.writeText(body.replaceFirst("@startuml", "@startuml\n$include"))
        }
    }
}

val generateArchitectureDiagrams by tasks.registering {
    dependsOn(exportArchitecture)
    inputs.files(fileTree("build/structurizr-export") { include("*.puml") }, pumlJar, "build.gradle.kts", "renderer-lock.json", "architecture/render-skin.puml")
    outputs.files(expectedNativeViews.map { diagrams.file("structurizr-$it.svg") })
    doLast {
        val sources = fileTree("build/structurizr-export") { include("*.puml") }.files.sorted()
        check(sources.size >= expectedNativeViews.size) { "Structurizr did not export all native C4 views" }
        val process = ProcessBuilder(listOf(javaCommand(), "-Djava.awt.headless=true", "-jar", pumlJar.asFile.absolutePath, "-tsvg", "-nometadata", "-charset", "UTF-8") + sources.map { it.absolutePath }).inheritIO().start()
        check(process.waitFor() == 0) { "C4 export rendering failed" }
        expectedNativeViews.forEach { key ->
            val source = file("build/structurizr-export/structurizr-$key.svg")
            check(source.isFile && source.length() > 100) { "Missing generated C4 SVG: $key" }
            copyVectorWithCanvas(source, diagrams.file(source.name).asFile)
        }
    }
}

val generateUiAssets by tasks.registering(Copy::class) {
    inputs.file("docs/ui-asset-manifest.json")
    from("ui-svg") { include("*.svg") }
    into("docs/screens")
    doFirst {
        val manifestIds = Regex("\"id\"\\s*:\\s*\"((?:SCR|STB)[0-9]+)\"").findAll(file("docs/ui-asset-manifest.json").readText()).map { it.groupValues[1] }.toList()
        check(manifestIds.size == manifestIds.toSet().size && manifestIds.toSet() == expectedUiViews.toSet()) { "UI manifest does not declare the exact required screens/storyboards" }
        check(fileTree("ui-svg") { include("*.svg") }.files.map { it.nameWithoutExtension }.toSet() == expectedUiViews.toSet()) { "Canonical UI source asset missing or unexpected" }
    }
    doLast {
        val exported = fileTree("docs/screens") { include("*.svg") }.files
        check(exported.map { it.nameWithoutExtension }.toSet() == expectedUiViews.toSet()) { "Exported UI assets differ from manifest" }
        exported.forEach { check(sha256(it) == sha256(file("ui-svg/${it.name}"))) { "Edit canonical ui-svg source, not generated copy: $it" } }
    }
}

val renderStoryboards by tasks.registering {
    dependsOn(generateUiAssets)
    inputs.files("storyboards/storyboard.md", ".tools/marp/package-lock.json", fileTree("docs/screens"))
    outputs.file("docs/storyboards.html")
    doLast {
        var source = file("storyboards/storyboard.md").readText()
        source = Regex("!\\[([^]]*)]\\(([^)]+\\.svg)\\)").replace(source) { match ->
            val image = file("storyboards").resolve(match.groupValues[2]).canonicalFile
            check(image.toPath().startsWith(projectDir.canonicalFile.toPath())) { "Storyboard image outside package" }
            "![${match.groupValues[1]}](data:image/svg+xml;base64,${Base64.getEncoder().encodeToString(image.readBytes())})"
        }
        val inline = layout.buildDirectory.file("storyboards.inline.md").get().asFile.apply { parentFile.mkdirs(); writeText(source) }
        val node = providers.environmentVariable("NODE_BIN").orElse("node").get()
        val process = ProcessBuilder(node, file(".tools/marp/node_modules/@marp-team/marp-cli/marp-cli.js").absolutePath, inline.absolutePath, "--html", "-o", file("docs/storyboards.html").absolutePath).inheritIO().start()
        val exit = process.waitFor()
        check(exit == 0) { "Pinned Marp slideshow rendering failed (exit=$exit)" }
        check(!Regex("(?:src|href)=[\"'](?:https?:)?//|url\\(\\s*[\"']?(?:https?:)?//", RegexOption.IGNORE_CASE).containsMatchIn(file("docs/storyboards.html").readText())) { "Slideshow contains external assets" }
    }
}

val verifyDesignDocs by tasks.registering {
    dependsOn(generateArchitectureDiagrams, generateUiAssets, renderStoryboards)
    inputs.files(fileTree("docs"), "README.md", "renderer-lock.json", "architecture/workspace.dsl", "architecture/code-diagrams.md", "storyboards/storyboard.md")
    outputs.file(layout.buildDirectory.file("receipts/docs-validation.txt"))
    doLast {
        val svgFiles = fileTree("docs") { include("**/*.svg") }.files
        val expectedSvgPaths = expectedNativeViews.map { "diagrams/structurizr-$it.svg" } + expectedCodeViews.map { "diagrams/$it.svg" } + expectedUiViews.map { "screens/$it.svg" }
        check(svgFiles.map { it.relativeTo(file("docs")).invariantSeparatorsPath }.toSet() == expectedSvgPaths.toSet()) { "Missing or unexpected vector diagram/screen assets" }
        svgFiles.forEach { svg ->
            val text = svg.readText()
            check(text.contains("<svg") && text.contains("<text")) { "SVG must contain vector text: $svg" }
            check(!text.contains("<!--SRC=[")) { "Stale embedded renderer metadata: $svg" }
            check(!text.contains("<image", ignoreCase = true) && !text.contains("<script", ignoreCase = true)) { "Raster/script not allowed in vector SVG: $svg" }
        }
        (fileTree("docs") { include("**/*.md") }.files + setOf(file("README.md"), file("storyboards/storyboard.md"), file("architecture/code-diagrams.md"))).forEach { md ->
            val text = md.readText()
            check(!text.contains("```mer" + "maid", ignoreCase = true)) { "Obsolete diagram source in current docs" }
            Regex("!?\\[[^]]*]\\(([^)]+)\\)").findAll(text).forEach { match ->
                val target = match.groupValues[1].substringBefore('#').trim('<', '>')
                if (target.isNotEmpty() && !target.contains("://") && !target.startsWith("data:")) check(md.parentFile.resolve(target).isFile) { "Broken relative link: $target in $md" }
            }
        }
        layout.buildDirectory.file("receipts/docs-validation.txt").get().asFile.apply { parentFile.mkdirs(); writeText("Vector SVGs=${svgFiles.size}; Markdown relative links valid; no raster substitutions; no public rendering\n") }
    }
}

val generateArtifactManifest by tasks.registering {
    dependsOn(verifyDesignDocs)
    inputs.files(fileTree("docs") { exclude("checksums.sha256") }, fileTree("architecture"), fileTree("ui-svg"), fileTree("storyboards"), fileTree("gradle"), fileTree("tooling"), "README.md", "build.gradle.kts", "settings.gradle.kts", "gradle.properties", "gradlew", "gradlew.bat", "toolchain-lock.json", "renderer-lock.json")
    outputs.file("docs/checksums.sha256")
    doLast {
        val files = (fileTree("docs") { exclude("checksums.sha256") }.files + fileTree("architecture").files + fileTree("ui-svg").files + fileTree("storyboards").files + fileTree("gradle").files + fileTree("tooling").files + listOf("README.md", "build.gradle.kts", "settings.gradle.kts", "gradle.properties", "gradlew", "gradlew.bat", "toolchain-lock.json", "renderer-lock.json").map { file(it) }).filter { !it.path.contains("__pycache__") }.sortedBy { it.relativeTo(projectDir).invariantSeparatorsPath }
        file("docs/checksums.sha256").writeText(files.joinToString("\n", postfix="\n") { "${sha256(it)}  ${it.relativeTo(projectDir).invariantSeparatorsPath}" })
    }
}

tasks.register<Zip>("packageDesignDocs") {
    dependsOn(generateArtifactManifest)
    archiveFileName.set("Android MCP Bridge Design Portable.zip")
    destinationDirectory.set(layout.buildDirectory.dir("distributions"))
    isPreserveFileTimestamps = false
    isReproducibleFileOrder = true
    from("docs") { into("docs") }
    from("architecture") { into("architecture") }
    from("ui-svg") { into("ui-svg") }
    from("storyboards") { into("storyboards") }
    from("gradle") { into("gradle") }
    from("tooling") { into("tooling"); exclude("**/__pycache__/**") }
    from("build/receipts") { into("validation") }
    from(listOf("README.md", "build.gradle.kts", "settings.gradle.kts", "gradle.properties", "gradlew", "gradlew.bat", "toolchain-lock.json", "renderer-lock.json"))
}
