export declare const enum Style {
    Bold = 1,
    Italic = 2,
    Mono = 4,
    Sub = 8,
    Sup = 16,
    Underline = 32
}
export interface TextMeasurer {
    width(text: string, size: number, style: number): number;
}
export declare const metricsMeasurer: TextMeasurer;
export declare function canvasMeasurer(fontFamily: string, monoFamily?: string): TextMeasurer | null;
export declare function defaultMeasurer(fontFamily?: string, monoFamily?: string): TextMeasurer;
export declare function forgetTextWidths(): void;
