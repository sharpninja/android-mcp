import { type Edit } from '../../errors.js';
import type { SankeyDb } from './db.js';
export declare function prepareTextForParsing(text: string, steps?: Edit[][]): string;
export declare function parseSankey(src: string, db: SankeyDb, source?: string): void;
