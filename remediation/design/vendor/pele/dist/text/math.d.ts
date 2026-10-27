import type { MathMarkup, MathResolver } from '../types.js';
export declare function withMath<T>(math: MathResolver | undefined, run: () => T): T;
export declare function mathActive(): boolean;
export declare function typeset(tex: string, size: number): MathMarkup | undefined;
export declare function withoutMath<T>(run: () => T): T;
