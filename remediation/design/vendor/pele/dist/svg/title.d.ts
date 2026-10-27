import { type Label } from '../text/label.js';
import { type TextMeasurer } from '../text/measurer.js';
export declare function titleLabel(title: string | undefined, measurer: TextMeasurer, size: number, maxWidth: number): Label;
export interface Titled {
    width: number;
    height: number;
    content: string;
}
export declare function withTitle(title: string | undefined, measurer: TextMeasurer, size: number, pad: number, width: number, height: number, content: string): Titled;
