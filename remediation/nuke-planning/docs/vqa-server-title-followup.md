# Server title follow-up after combined native trial

The `vqa-combined-native-001` run completed successfully with 27 architecture artifacts and 138 browser captures. Direct original-detail inspection leaves VQA-DIAGRAM-001 open. The server PNG is byte-identical to its original baseline, SHA256 `ca9004babef1ba452dec2fd5b3e472d3f57009af6a571347088871709acd1ec7`. The generated PUML contains the requested local title style, so extraction did not drop it.

The title alignment hypothesis was incorrect for this pinned renderer. PlantUML 1.2025.10's [AnnotatedWorker.addTitle](https://github.com/plantuml/plantuml/blob/v1.2025.10/src/main/java/net/sourceforge/plantuml/AnnotatedWorker.java#L72) places the entire title block at the center. The `HorizontalAlignment` style controls alignment within the text block. That explains why changing it for a single line did not move the title to the left.

The pinned [Smetana bounds calculation](https://github.com/plantuml/plantuml/blob/v1.2025.10/src/main/java/net/sourceforge/plantuml/sdot/CucaDiagramFileMakerSmetana.java#L130) includes nodes and clusters, while edges are drawn afterward. This supports the diagnosis that the long upper relationship extends into the title area. It is an inference from source and the observed SVG, not a newly executed renderer experiment.

The next isolated candidate replaces the ineffective alignment setting with bottom spacing around the title:

```plantuml
<style>
title {
  Margin 0 0 96 0
}
</style>
```

The pinned [title builder](https://github.com/plantuml/plantuml/blob/v1.2025.10/src/main/java/net/sourceforge/plantuml/AnnotatedBuilder.java#L153) uses the styled title block. Its [Style implementation](https://github.com/plantuml/plantuml/blob/v1.2025.10/src/main/java/net/sourceforge/plantuml/style/Style.java#L278) applies margins, and the [four-value parser](https://github.com/plantuml/plantuml/blob/v1.2025.10/src/main/java/net/sourceforge/plantuml/style/ClockwiseTopRightBottomLeft.java#L52) reads top, right, bottom, left. This candidate preserves the title text, font, every class/member, every relationship and multiplicity. It changes spacing only. The 96-unit value is a trial allowance for the observed upper-route protrusion, not a proven minimum.

No second trial has run. A future normal render and direct PNG inspection must establish whether the spacing resolves the finding. No canonical input, golden output, or app source is changed by this proposal. The successful Deployment direction trial remains separate and is not evidence that the server title is fixed.

The directly observed run identities and separate visual findings are retained in [the inspection receipt](evidence/vqa-combined-native-001-visual-inspection.json), SHA256 `916c1dbddbe16e29179a0fc5de2936c6b13dc7572322249457db633d7fa739d4`.
