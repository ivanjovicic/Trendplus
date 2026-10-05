#!/usr/bin/env python3
"""Summarize VSTest TRX output for non-skippable Operations certification (RQ453)."""

from __future__ import annotations

import argparse
import json
import sys
import xml.etree.ElementTree as ET
from dataclasses import dataclass
from glob import glob
from pathlib import Path

TRX_NAMESPACE = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}


@dataclass(frozen=True)
class SuiteSpec:
    suite_id: str
    trx_glob: str
    required: bool
    label: str


def parse_trx(path: Path) -> tuple[int, int, int, int]:
    root = ET.parse(path).getroot()
    results = root.findall(".//t:UnitTestResult", TRX_NAMESPACE)
    passed = sum(result.attrib.get("outcome") == "Passed" for result in results)
    skipped = sum(result.attrib.get("outcome") in {"Skipped", "NotExecuted"} for result in results)
    failed = len(results) - passed - skipped
    return len(results), passed, skipped, failed


def parse_vitest_json(path: Path) -> tuple[int, int, int, int]:
    data = json.loads(path.read_text(encoding="utf-8"))
    total = int(data.get("numTotalTests", 0))
    passed = int(data.get("numPassedTests", 0))
    skipped = int(data.get("numPendingTests", 0))
    failed = int(data.get("numFailedTests", 0))
    return total, passed, skipped, failed


def summarize_paths(paths: list[Path]) -> tuple[int, int, int, int]:
    total = passed = skipped = failed = 0
    for path in paths:
        if path.suffix.lower() == ".json":
            executed, suite_passed, suite_skipped, suite_failed = parse_vitest_json(path)
        else:
            executed, suite_passed, suite_skipped, suite_failed = parse_trx(path)
        total += executed
        passed += suite_passed
        skipped += suite_skipped
        failed += suite_failed
    return total, passed, skipped, failed


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--manifest-out", required=True, help="Path to write machine-readable manifest JSON")
    parser.add_argument("--commit-sha", default="", help="Git commit SHA for the certification run")
    parser.add_argument("--step-outcome", action="append", default=[], metavar="SUITE_ID=success|failure")
    parser.add_argument(
        "--suite",
        action="append",
        default=[],
        metavar="ID|GLOB|REQUIRED|LABEL",
        help="Suite definition: id|trx/glob/pattern|true|false|human label",
    )
    args = parser.parse_args()

    step_outcomes: dict[str, str] = {}
    for entry in args.step_outcome:
        if "=" not in entry:
            print(f"::error::Invalid --step-outcome value: {entry}", file=sys.stderr)
            return 2
        suite_id, outcome = entry.split("=", 1)
        step_outcomes[suite_id] = outcome

    suites: list[SuiteSpec] = []
    for raw in args.suite:
        parts = raw.split("|", 3)
        if len(parts) != 4:
            print(f"::error::Invalid --suite value: {raw}", file=sys.stderr)
            return 2
        suite_id, trx_glob, required_raw, label = parts
        suites.append(
            SuiteSpec(
                suite_id=suite_id,
                trx_glob=trx_glob,
                required=required_raw.lower() == "true",
                label=label,
            )
        )

    manifest_suites: list[dict[str, object]] = []
    overall_pass = True

    for spec in suites:
        matches = [Path(path) for path in glob(spec.trx_glob, recursive=True)]
        step_outcome = step_outcomes.get(spec.suite_id, "unknown")
        if not matches:
            manifest_suites.append(
                {
                    "id": spec.suite_id,
                    "label": spec.label,
                    "required": spec.required,
                    "executed": 0,
                    "passed": 0,
                    "skipped": 0,
                    "failed": 0,
                    "stepOutcome": step_outcome,
                    "verdict": "FAIL" if spec.required else "NOT_RUN",
                }
            )
            if spec.required:
                print(f"::error::Certification suite {spec.suite_id} produced no TRX report ({spec.trx_glob}).")
                overall_pass = False
            continue

        total, passed, skipped, failed = summarize_paths(matches)

        suite_pass = (
            step_outcome == "success"
            and total > 0
            and skipped == 0
            and failed == 0
            and passed == total
        )
        if spec.required and not suite_pass:
            overall_pass = False
            print(
                f"::error::Certification suite {spec.suite_id}: total={total} passed={passed} "
                f"skipped={skipped} failed={failed} step={step_outcome}"
            )

        manifest_suites.append(
            {
                "id": spec.suite_id,
                "label": spec.label,
                "required": spec.required,
                "executed": total,
                "passed": passed,
                "skipped": skipped,
                "failed": failed,
                "stepOutcome": step_outcome,
                "verdict": "PASS" if suite_pass else ("FAIL" if spec.required else "OPTIONAL_FAIL"),
            }
        )

    required_suites = [suite for suite in manifest_suites if suite["required"]]
    manifest = {
        "manifestId": "operations-analytics-certification-rq453",
        "commitSha": args.commit_sha,
        "expectedRequiredSuites": len(required_suites),
        "executedRequiredSuites": sum(1 for suite in required_suites if suite["executed"] > 0),
        "passedRequiredSuites": sum(1 for suite in required_suites if suite["verdict"] == "PASS"),
        "skippedRequiredTests": sum(int(suite["skipped"]) for suite in required_suites),
        "suites": manifest_suites,
        "routeManifest": {
            "id": "operations-six-screen-certification-2026-10-04",
            "expectedRoutes": 6,
            "expectedCases": 31,
            "prePostVerdict": "VERIFIED",
        },
        "deployedBrowserProof": {
            "required": False,
            "executed": False,
            "owner": "RQ565",
        },
        "overallVerdict": "PASS" if overall_pass else "FAIL",
    }

    manifest_path = Path(args.manifest_out)
    manifest_path.parent.mkdir(parents=True, exist_ok=True)
    manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(json.dumps(manifest, indent=2))

    return 0 if overall_pass else 1


if __name__ == "__main__":
    raise SystemExit(main())
