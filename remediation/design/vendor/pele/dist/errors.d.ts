export type PeleErrorCode = 'unsupported-diagram' | 'syntax' | 'semantic' | 'limit';
export declare class PeleError extends Error {
    code: PeleErrorCode;
    type: string | null;
    mermaidType: string | null;
    line: number;
    column: number;
    snippet: string;
    constructor(message: string, code?: PeleErrorCode, info?: {
        type?: string | null;
        mermaidType?: string | null;
        line?: number;
        column?: number;
        snippet?: string;
    });
}
export type Edit = [from: number, to: number, length: number];
export declare function replacements(text: string, re: RegExp, length: number): Edit[];
export declare function inputOffset(steps: Edit[][], at: number): number;
interface Origin {
    parsed: string;
    find(offset: number): [source: string, offset: number];
}
export declare function locatingIn<T>(next: Origin, run: () => T): T;
export declare function written(src: string, offset: number): [source: string, offset: number];
export declare function locate(src: string, offset: number): {
    line: number;
    column: number;
    snippet: string;
};
export declare function syntaxError(type: string, src: string, offset: number, detail: string, lexical?: boolean): PeleError;
export {};
