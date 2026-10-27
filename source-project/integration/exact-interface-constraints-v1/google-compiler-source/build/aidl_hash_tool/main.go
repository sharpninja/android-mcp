// Copyright 2026 Google Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

package main

import (
	"bufio"
	"crypto/sha1"
	"flag"
	"fmt"
	"io"
	"os"
	"path/filepath"
	"slices"
	"sort"
	"strings"

	"android/soong/response"
)

func main() {
	flag.Usage = func() {
		fmt.Fprintf(os.Stderr, "Usage: %s <command> ...\n", os.Args[0])
		fmt.Fprintf(os.Stderr, "commands:\n")
		fmt.Fprintf(os.Stderr, "  gen_hash <input_list_file> <api_dir> <version> <out>: Generates a hash and writes it to out\n")
		fmt.Fprintf(os.Stderr, "  verify_hash <input_list_file> <api_dir> <version> <hash_file> <out> <message_file>: Verifies a hash matches the one in the hash_file, and touches out if so.\n")
	}
	flag.Parse()
	args := flag.Args()

	if len(args) < 4 {
		flag.Usage()
		os.Exit(1)
	}

	command := args[0]
	inputListFile := args[1]
	apiDir := args[2]
	version := args[3]

	// Read and .aidl files from inputListFile
	f := must2(os.Open(inputListFile))
	files := must2(response.ReadRspFile(f))
	must(f.Close())

	switch command {
	case "gen_hash":
		if len(args) != 5 {
			flag.Usage()
			os.Exit(1)
		}
		out := args[4]
		hash := create_hash(apiDir, version, files)
		must(os.WriteFile(out, []byte(hash+"\n"), 0666))
	case "verify_hash":
		if len(args) != 7 {
			flag.Usage()
			os.Exit(1)
		}
		hashFile := args[4]
		outFile := args[5]
		messageFile := args[6]

		actualHash := create_hash(apiDir, version, files)

		expectedHash := readLastLine(hashFile)

		if actualHash == expectedHash {
			// update mtime of timestamp file
			must(os.WriteFile(outFile, []byte{}, 0666))
		} else {
			fmt.Print(string(must2(os.ReadFile(messageFile))))
			os.Exit(1)
		}
	default:
		flag.Usage()
		os.Exit(1)
	}
}

// create_hash returns a hash of all files under apiDir in the format that has historically
// been used for aidl api dumps.
func create_hash(apiDir string, version string, files []string) string {
	// Make the files relative to apiDir and start with ./. The files must be in this exact format.
	files = slices.Clone(files)
	for i, f := range files {
		rel := must2(filepath.Rel(apiDir, f))
		files[i] = "./" + rel
	}
	sort.Strings(files)
	// Compute hashes of files and combine with version. This has to be careful to match
	// the exact hash format that was used historically with this shell command:
	// cd '${apiDir}' && { find ./ -name "*.aidl" -print0 | LC_ALL=C sort -z | xargs -0 sha1sum && echo ${version}; } | sha1sum | cut -d " " -f 1
	h := sha1.New()
	for _, file := range files {
		f := must2(os.Open(filepath.Join(apiDir, file)))
		fh := sha1.New()
		must2(io.Copy(fh, f))
		f.Close()
		fmt.Fprintf(h, "%x  %s\n", fh.Sum(nil), file)
	}
	fmt.Fprintln(h, version)
	return fmt.Sprintf("%x", h.Sum(nil))
}

func readLastLine(path string) string {
	file := must2(os.Open(path))
	defer file.Close()

	var lastLine string
	scanner := bufio.NewScanner(file)
	for scanner.Scan() {
		line := strings.TrimSpace(scanner.Text())
		if line != "" {
			lastLine = line
		}
	}
	must(scanner.Err())
	return lastLine
}

func must(err error) {
	if err != nil {
		fmt.Fprintf(os.Stderr, "%s\n", err)
		os.Exit(1)
	}
}

func must2[T any](x T, err error) T {
	if err != nil {
		fmt.Fprintf(os.Stderr, "%s\n", err)
		os.Exit(1)
	}
	return x
}
