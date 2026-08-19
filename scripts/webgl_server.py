#!/usr/bin/env python3
import argparse
import http.server
import os


class UnityWebGlHandler(http.server.SimpleHTTPRequestHandler):
    def end_headers(self):
        if self.path.split("?", 1)[0].endswith(".br"):
            self.send_header("Content-Encoding", "br")
        super().end_headers()

    def guess_type(self, path):
        uncompressed = path[:-3] if path.endswith(".br") else path
        if uncompressed.endswith(".wasm"):
            return "application/wasm"
        if uncompressed.endswith(".js"):
            return "application/javascript"
        if uncompressed.endswith(".data"):
            return "application/octet-stream"
        return super().guess_type(uncompressed)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--directory", required=True)
    parser.add_argument("--port", type=int, required=True)
    args = parser.parse_args()
    os.chdir(args.directory)
    server = http.server.ThreadingHTTPServer(("127.0.0.1", args.port), UnityWebGlHandler)
    print(f"CF_WEBGL_SERVER_READY port={args.port}", flush=True)
    server.serve_forever()


if __name__ == "__main__":
    main()
