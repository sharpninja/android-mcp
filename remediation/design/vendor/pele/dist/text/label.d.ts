import { type TextMeasurer } from './measurer.js';
import type { MathMarkup } from '../types.js';
export interface Span {
    text: string;
    style: number;
    icon?: string;
    math?: MathMarkup;
    width: number;
}
export interface Label {
    lines: Span[][];
    widths: number[];
    width: number;
    height: number;
    size: number;
    lineHeight: number;
    heights?: number[];
}
export declare const LINE_HEIGHT = 1.5;
export declare function layoutLabel(raw: string | undefined, markdown: boolean, measurer: TextMeasurer, size: number, maxWidth: number, base?: number): Label;
export declare function withHeights(label: Label): Label;
export declare function hasMath(line: Span[]): boolean;
export declare function lineMiddle(label: Label, i: number): number;
export declare function labelLine(label: Label, i: number): Label;
