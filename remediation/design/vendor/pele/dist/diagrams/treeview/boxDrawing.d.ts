export interface PreprocessResult {
    text: string;
    lineMap: Map<number, number>;
    columns: Map<number, (at: number) => number>;
}
export declare function isBoxDrawingFormat(lines: string[]): boolean;
export declare function remapErrorLines(message: string, lineMap: Map<number, number>): string;
export declare function lineStart(text: string, line: number): number;
export declare function preprocessBoxDrawing(input: string): PreprocessResult;
