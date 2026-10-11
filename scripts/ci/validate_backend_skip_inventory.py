#!/usr/bin/env python3
"""Fail when the backend suite's Operations skip population drifts unclassified."""

import argparse
import json
import sys
import xml.etree.ElementTree as ET
from collections import Counter
from pathlib import Path


NAMESPACE = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
SKIPPED_OUTCOMES = {"Skipped", "NotExecuted"}
ALLOWED_CLASSIFICATIONS = {
    "isolated-postgresql-runnable",
    "opt-in-external/provider",
    "legacy-superseded",
    "genuinely-uncovered",
}


def fail(message: str) -> None:
    print(f"::error::{message}", file=sys.stderr)
    raise SystemExit(1)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--trx", required=True, type=Path)
    parser.add_argument("--inventory", required=True, type=Path)
    args = parser.parse_args()

    inventory = json.loads(args.inventory.read_text(encoding="utf-8"))
    entries = inventory.get("entries")
    if not isinstance(entries, list):
        fail("Backend skip inventory must contain an entries array.")

    expected: dict[str, dict] = {}
    for entry in entries:
        fqn = entry.get("fullyQualifiedName")
        display_name = entry.get("displayName")
        classification = entry.get("classification")
        if not fqn or not display_name:
            fail("Every skip inventory entry needs fullyQualifiedName and displayName.")
        if classification not in ALLOWED_CLASSIFICATIONS:
            fail(f"{fqn}: unsupported/missing classification {classification!r}.")
        if not entry.get("skipReason") or not entry.get("source", {}).get("runUrl"):
            fail(f"{fqn}: missing exact skip reason or source run link.")
        if classification == "legacy-superseded" and not entry.get("proof"):
            fail(f"{fqn}: superseded classification needs a named replacement proof.")
        if classification == "genuinely-uncovered" and not entry.get("unblockEvent"):
            fail(f"{fqn}: uncovered classification needs an explicit unblock event.")
        key = f"{fqn}|{display_name}"
        if key in expected:
            fail(f"Duplicate skip inventory entry: {key}")
        expected[key] = entry

    if len(expected) != inventory.get("expectedKnownSkippedCount"):
        fail(
            "Inventory expectedKnownSkippedCount does not match entries: "
            f"{inventory.get('expectedKnownSkippedCount')} vs {len(expected)}."
        )

    root = ET.parse(args.trx).getroot()
    definitions: dict[str, tuple[str, str]] = {}
    for test in root.findall(".//t:TestDefinitions/t:UnitTest", NAMESPACE):
        method = test.find("t:TestMethod", NAMESPACE)
        if method is None:
            continue
        definitions[test.attrib["id"]] = (
            method.attrib.get("className", ""),
            method.attrib.get("name", ""),
        )

    actual: dict[str, dict] = {}
    for result in root.findall(".//t:Results/t:UnitTestResult", NAMESPACE):
        if result.attrib.get("outcome") not in SKIPPED_OUTCOMES:
            continue
        test_id = result.attrib.get("testId", "")
        if test_id not in definitions:
            fail(f"Skipped TRX result {result.attrib.get('testName')} has no test definition.")
        class_name, method_name = definitions[test_id]
        fqn = f"{class_name}.{method_name}"
        key = f"{fqn}|{result.attrib.get('testName', '')}"
        message = result.find("t:Output/t:ErrorInfo/t:Message", NAMESPACE)
        actual[key] = {
            "fqn": fqn,
            "displayName": result.attrib.get("testName", ""),
            "reason": (message.text or "").strip() if message is not None else "",
        }

    unexpected = sorted(set(actual) - set(expected))
    missing = sorted(set(expected) - set(actual))
    reason_mismatch = [
        key
        for key in sorted(set(actual) & set(expected))
        if actual[key]["reason"] != expected[key]["skipReason"]
    ]
    if unexpected:
        fail("New/unclassified backend test skips: " + "; ".join(unexpected))
    if missing:
        fail("Known backend skip inventory drift (expected skips no longer present): " + "; ".join(missing))
    if reason_mismatch:
        fail("Backend skip reason drift: " + "; ".join(reason_mismatch))

    counts = Counter(entry["classification"] for entry in entries)
    summary = (
        "## Backend skip inventory\n\n"
        f"- Known skipped tests: **{len(actual)}** (all individually classified)\n"
        f"- Isolated PostgreSQL certifiable: **{counts['isolated-postgresql-runnable']}**\n"
        f"- Legacy superseded by named proof: **{counts['legacy-superseded']}**\n"
        f"- Genuinely uncovered: **{counts['genuinely-uncovered']}**\n"
        f"- Opt-in external/provider: **{counts['opt-in-external/provider']}**\n"
        "- New, missing, or reason-changed skips: **0**\n"
    )
    print(summary, end="")
    summary_path = Path(__import__("os").environ.get("GITHUB_STEP_SUMMARY", ""))
    if str(summary_path) and summary_path.is_file():
        with summary_path.open("a", encoding="utf-8") as stream:
            stream.write(summary)


if __name__ == "__main__":
    main()
