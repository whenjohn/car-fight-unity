#!/usr/bin/env python3
"""Deterministic two-client UDP shaper for the native FishNet gate."""

import argparse
import heapq
import random
import selectors
import signal
import socket
import time


def arguments():
    parser = argparse.ArgumentParser()
    parser.add_argument("--server-port", type=int, required=True)
    parser.add_argument("--alpha-port", type=int, required=True)
    parser.add_argument("--bravo-port", type=int, required=True)
    parser.add_argument("--latency-ms", type=float, default=0)
    parser.add_argument("--jitter-ms", type=float, default=0)
    parser.add_argument("--loss-percent", type=float, default=0)
    parser.add_argument("--seed", type=int, required=True)
    parser.add_argument("--run-id", required=True)
    return parser.parse_args()


class Path:
    def __init__(self, name, listen_port, server_port, selector):
        self.name = name
        self.client = None
        self.front = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        self.front.bind(("127.0.0.1", listen_port))
        self.front.setblocking(False)
        self.back = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        self.back.bind(("127.0.0.1", 0))
        self.back.setblocking(False)
        self.server = ("127.0.0.1", server_port)
        selector.register(self.front, selectors.EVENT_READ, (self, "up"))
        selector.register(self.back, selectors.EVENT_READ, (self, "down"))


def main():
    args = arguments()
    selector = selectors.DefaultSelector()
    paths = [
        Path("alpha", args.alpha_port, args.server_port, selector),
        Path("bravo", args.bravo_port, args.server_port, selector),
    ]
    randomizer = random.Random(args.seed)
    queue = []
    serial = 0
    counters = {"forwarded": 0, "delayed": 0, "reordered": 0, "dropped": 0}
    last_due = {}
    running = True

    def stop(_signum, _frame):
        nonlocal running
        running = False

    signal.signal(signal.SIGTERM, stop)
    signal.signal(signal.SIGINT, stop)
    print(
        f"CF_PROXY event=PROXY_READY run_id={args.run_id} "
        f"alpha_port={args.alpha_port} bravo_port={args.bravo_port}",
        flush=True,
    )

    while running:
        now = time.monotonic()
        while queue and queue[0][0] <= now:
            _due, _serial, output, payload, target = heapq.heappop(queue)
            output.sendto(payload, target)
            counters["forwarded"] += 1

        timeout = 0.02
        if queue:
            timeout = max(0, min(timeout, queue[0][0] - now))
        for key, _mask in selector.select(timeout):
            path, direction = key.data
            payload, source = key.fileobj.recvfrom(65535)
            if direction == "up":
                path.client = source
                output, target = path.back, path.server
            else:
                if path.client is None:
                    continue
                output, target = path.front, path.client

            if randomizer.random() * 100 < args.loss_percent:
                counters["dropped"] += 1
                continue
            jitter = randomizer.uniform(-args.jitter_ms, args.jitter_ms)
            delay = max(0.0, args.latency_ms + jitter) / 1000.0
            due = time.monotonic() + delay
            ordering_key = (path.name, direction)
            if ordering_key in last_due and due < last_due[ordering_key]:
                counters["reordered"] += 1
            last_due[ordering_key] = due
            if delay > 0:
                counters["delayed"] += 1
                serial += 1
                heapq.heappush(queue, (due, serial, output, payload, target))
            else:
                output.sendto(payload, target)
                counters["forwarded"] += 1

    print(
        "CF_PROXY event=PROXY_COUNTERS "
        + " ".join(f"{name}={value}" for name, value in counters.items()),
        flush=True,
    )


if __name__ == "__main__":
    main()
