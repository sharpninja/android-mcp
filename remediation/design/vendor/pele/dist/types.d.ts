import type { DiagramType, MermaidType } from './detect.js';
import type { Config } from './preprocess.js';
import type { TextMeasurer } from './text/measurer.js';
export type IconResolver = (name: string) => string | null | undefined;
export type MathMarkup = {
    svg: string;
    width: number;
    height: number;
} | {
    html: string;
    width: number;
    height: number;
};
export type MathResolver = (tex: string, size: number) => MathMarkup | null | undefined;
export interface RenderOptions {
    measurer?: TextMeasurer;
    fontFamily?: string;
    fontFamilyMono?: string;
    fontSize?: number;
    idPrefix?: string;
    responsive?: boolean;
    maxWidth?: number;
    autoDirection?: boolean;
    directionBreakpoint?: number;
    padding?: number;
    limit?: number;
    outputLimit?: number;
    maxEdges?: number;
    linkSchemes?: string[];
    imageSchemes?: string[];
    links?: boolean;
    images?: boolean;
    linkRel?: string;
    icons?: IconResolver;
    math?: MathResolver;
    config?: Config;
    now?: number | Date;
}
export interface LinkInfo {
    id: string;
    href: string;
    internal: boolean;
}
export interface Rendered {
    svg: string;
    width: number;
    height: number;
    links: LinkInfo[];
}
export interface RenderResult extends Rendered {
    type: DiagramType;
    mermaidType: MermaidType;
}
export interface ParseLimits {
    maxEdges: number;
}
export interface Diagram<Model> {
    type: DiagramType;
    section?: string;
    keepComments?: boolean;
    math?: boolean;
    parse(source: string, config: Config, title: string | undefined, lineOffset: number, limits: ParseLimits): Model;
    render(model: Model, config: Config, options: RenderOptions): Rendered;
}
