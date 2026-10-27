import { type Edit } from './errors.js';
import { type YamlValue } from './util/yaml.js';
export type Config = {
    [key: string]: YamlValue;
};
export interface Preprocessed {
    text: string;
    title: string | undefined;
    config: Config;
    withComments: string;
    lineOffset: number;
}
export declare function cleanupComments(text: string, steps?: Edit[][]): string;
export declare function normalizeLineBreaks(text: string): string;
export declare function preprocess(source: string, steps?: Edit[][]): Preprocessed;
export declare function encodeEntities(text: string, steps?: Edit[][]): string;
