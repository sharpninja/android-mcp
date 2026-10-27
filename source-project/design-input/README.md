# Android MCP bridge design portable edition

Open **Android MCP Bridge Design.md** in a Markdown viewer with local SVG support. Keep this folder structure intact: its two diagram links are relative to `assets/`. If a viewer blocks SVG, open the SVG directly in a browser. The Word edition embeds rasterized versions of the same diagrams for office compatibility.

This is a design document, not an installed server, device connection or application implementation.

## Edit and render diagrams

Editable Mermaid sources are in `sources/`. Both diagrams were rendered with the official npm package **pele 1.10.0**. The lock file pins the distribution and integrity hash. With Node.js 20 or newer:

```sh
npm ci --ignore-scripts
npm run render
```

These commands regenerate local SVGs only. No hosted service, CDN, external images or private URLs are needed to view the included diagrams. Font choice is DejaVu Sans; install that font for the closest editable regeneration. CSS theme variables are resolved to standalone values. Links and images are disabled in the renderer and the output is checked for active/external content.

The document also retains Mermaid source blocks after each SVG image, so editing does not depend on a specific Markdown plugin. If you edit a source file, synchronize its fenced block in the Markdown after rendering. `render-manifest.json` records the verified renderer and options; `checksums.sha256` identifies the shipped files.

Official renderer documentation: [API](https://pele.run/api), [compatibility](https://pele.run/compatibility), [security](https://pele.run/security). Pele licensing is included in `PELE-LICENSE.txt`.
