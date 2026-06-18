import csv
import re
import sys
import unicodedata
from collections import Counter, defaultdict
from pathlib import Path


def iter_statements(text):
    buffer = []
    in_string = False
    index = 0

    while index < len(text):
        character = text[index]
        buffer.append(character)

        if in_string:
            if character == "'":
                if index + 1 < len(text) and text[index + 1] == "'":
                    buffer.append(text[index + 1])
                    index += 2
                    continue

                in_string = False
        else:
            if character == "'":
                in_string = True
            elif character == ";":
                statement = "".join(buffer).strip()
                if statement:
                    yield statement
                buffer = []

        index += 1

    statement = "".join(buffer).strip()
    if statement:
        yield statement


def split_values(statement):
    marker = "VALUES ("
    start = statement.find(marker)
    if start < 0:
        return []

    index = start + len(marker)
    body = statement[index:statement.rfind(")")]
    values = []
    buffer = []
    in_string = False
    position = 0

    while position < len(body):
        character = body[position]

        if in_string:
            if character == "'":
                if position + 1 < len(body) and body[position + 1] == "'":
                    buffer.append("'")
                    position += 2
                    continue

                in_string = False
            else:
                buffer.append(character)
        else:
            if character == "'":
                in_string = True
            elif character == ",":
                values.append("".join(buffer).strip())
                buffer = []
            else:
                buffer.append(character)

        position += 1

    values.append("".join(buffer).strip())
    return [None if value == "NULL" else value for value in values]


def normalize(value):
    if not value:
        return ""

    value = value.lower()
    value = unicodedata.normalize("NFD", value)
    value = "".join(character for character in value if unicodedata.category(character) != "Mn")
    return re.sub(r"\s+", " ", unicodedata.normalize("NFC", value)).strip()


def contains_phrase(normalized_text, normalized_phrase):
    if not normalized_phrase:
        return False

    if any(character.isalnum() for character in normalized_phrase):
        safe = re.escape(normalized_phrase).replace(r"\ ", r"\s+")
        return re.search(r"(^|[^a-z0-9+#.])" + safe + r"($|[^a-z0-9+#.])", normalized_text, re.I) is not None

    return normalized_phrase.lower() in normalized_text.lower()


def percentage(numerator, denominator):
    return 0.0 if denominator == 0 else round(numerator * 100 / denominator, 2)


def parse_criterion_rules(rules_path):
    rules_text = rules_path.read_text(encoding="utf-8", errors="replace")
    start = rules_text.find("private static readonly CriterionRule[] CriterionRules")
    end = rules_text.find("};", start)
    rules = []

    for line in rules_text[start:end].splitlines():
        stripped = line.strip()
        if not stripped.startswith("new("):
            continue

        parts = re.findall(r'"((?:[^"\\]|\\.)*)"', stripped)
        if len(parts) < 4:
            continue

        kind, code, display, *aliases = [part.replace(r"\"", '"') for part in parts]
        rules.append({"kind": kind, "code": code, "display": display, "aliases": aliases})

    return rules


def main():
    if len(sys.argv) < 2:
        raise SystemExit("Usage: analyze_backup_extraction.py <Job_platform.sql>")

    root = Path.cwd()
    dump_path = Path(sys.argv[1])
    rules_path = root / "MauiApp1.Importer" / "JobClassificationRules.cs"
    report_path = root / "analysis_job_platform_extraction_audit.csv"

    sql_text = dump_path.read_text(encoding="utf-8", errors="replace")
    interesting_tables = {
        "job_offers",
        "job_criteria",
        "job_offer_criteria",
        "job_categories",
        "job_roles",
        "job_offer_roles",
        "job_offer_tags",
        "job_offer_languages",
        "job_sources",
    }
    insert_pattern = re.compile(r"^INSERT INTO public\.([a-z_]+)", re.S)
    rows = defaultdict(list)

    for statement in iter_statements(sql_text):
        match = insert_pattern.match(statement)
        if match and match.group(1) in interesting_tables:
            rows[match.group(1)].append(split_values(statement))

    criterion_rules = parse_criterion_rules(rules_path)
    criteria = {
        int(row[0]): {"kind": row[1], "code": row[2], "display": row[3]}
        for row in rows["job_criteria"]
    }
    categories = {
        int(row[0]): {"code": row[2], "display": row[3]}
        for row in rows["job_categories"]
    }
    roles = {
        int(row[0]): {"category_id": None if row[1] is None else int(row[1]), "code": row[2], "display": row[3]}
        for row in rows["job_roles"]
    }
    sources = {
        int(row[0]): {"code": row[1], "display": row[2]}
        for row in rows["job_sources"]
    }

    offers = {}
    for row in rows["job_offers"]:
        offer_id = int(row[0])
        offers[offer_id] = {
            "source_id": int(row[1]),
            "external_id": row[2],
            "title": row[4] or "",
            "company": row[5] or "",
            "description": row[11] or "",
            "active": row[25] == "true",
        }

    active_ids = {offer_id for offer_id, offer in offers.items() if offer["active"]}

    tags_by_offer = defaultdict(list)
    for row in rows["job_offer_tags"]:
        offer_id = int(row[0])
        if offer_id in active_ids and row[1]:
            tags_by_offer[offer_id].append(row[1])

    roles_by_offer = defaultdict(set)
    for row in rows["job_offer_roles"]:
        offer_id = int(row[0])
        role_id = int(row[1])
        if offer_id in active_ids:
            roles_by_offer[offer_id].add(role_id)

    category_by_offer = defaultdict(set)
    orphan_role_links = 0
    for offer_id, role_ids in roles_by_offer.items():
        for role_id in role_ids:
            if role_id not in roles:
                orphan_role_links += 1
                continue

            category_id = roles[role_id]["category_id"]
            if category_id in categories:
                category_by_offer[offer_id].add(categories[category_id]["code"])

    criteria_by_offer = defaultdict(set)
    criteria_detail_by_offer = defaultdict(list)
    for row in rows["job_offer_criteria"]:
        offer_id = int(row[0])
        criterion_id = int(row[1])
        if offer_id not in active_ids or criterion_id not in criteria:
            continue

        item = criteria[criterion_id]
        key = (item["kind"], item["code"])
        level = row[5] or ("required" if row[2] == "true" else "unknown")
        criteria_by_offer[offer_id].add(key)
        criteria_detail_by_offer[offer_id].append(
            {"kind": item["kind"], "code": item["code"], "is_required": row[2] == "true", "level": level}
        )

    expected_by_offer = defaultdict(set)
    for offer_id in active_ids:
        source_text = " ".join(
            [
                offers[offer_id]["title"],
                offers[offer_id]["description"],
                " ".join(tags_by_offer[offer_id]),
            ]
        )
        normalized_text = normalize(source_text)

        for rule in criterion_rules:
            for alias in rule["aliases"]:
                if contains_phrase(normalized_text, normalize(alias)):
                    expected_by_offer[offer_id].add((rule["kind"], rule["code"]))
                    break

    all_expected_pairs = {
        (offer_id, key)
        for offer_id, keys in expected_by_offer.items()
        for key in keys
    }
    all_extracted_pairs = {
        (offer_id, key)
        for offer_id, keys in criteria_by_offer.items()
        for key in keys
    }
    true_positive = all_expected_pairs & all_extracted_pairs
    missing_pairs = all_expected_pairs - all_extracted_pairs
    extra_pairs = all_extracted_pairs - all_expected_pairs

    by_kind = {}
    all_kinds = sorted({rule["kind"] for rule in criterion_rules} | {key[0] for _, key in all_extracted_pairs})
    for kind in all_kinds:
        expected = {(offer_id, key) for offer_id, key in all_expected_pairs if key[0] == kind}
        extracted = {(offer_id, key) for offer_id, key in all_extracted_pairs if key[0] == kind}
        matched = expected & extracted
        by_kind[kind] = {
            "expected": len(expected),
            "extracted": len(extracted),
            "matched": len(matched),
            "missing": len(expected - extracted),
            "extra": len(extracted - expected),
            "recall": percentage(len(matched), len(expected)),
            "precision": percentage(len(matched), len(extracted)),
        }

    it_ids = {offer_id for offer_id, categories_for_offer in category_by_offer.items() if "it" in categories_for_offer}
    legacy_it_ids = it_ids | {
        offer_id
        for offer_id, role_ids in roles_by_offer.items()
        if 8 in role_ids
    }
    it_expected = {
        (offer_id, key)
        for offer_id, key in all_expected_pairs
        if offer_id in it_ids and key[0] == "technology"
    }
    it_extracted = {
        (offer_id, key)
        for offer_id, key in all_extracted_pairs
        if offer_id in it_ids and key[0] == "technology"
    }
    it_matched = it_expected & it_extracted
    legacy_it_expected = {
        (offer_id, key)
        for offer_id, key in all_expected_pairs
        if offer_id in legacy_it_ids and key[0] == "technology"
    }
    legacy_it_extracted = {
        (offer_id, key)
        for offer_id, key in all_extracted_pairs
        if offer_id in legacy_it_ids and key[0] == "technology"
    }
    legacy_it_matched = legacy_it_expected & legacy_it_extracted

    per_offer_rows = []
    for offer_id in sorted(active_ids):
        expected = expected_by_offer[offer_id]
        extracted = criteria_by_offer[offer_id]
        matched = expected & extracted
        missing = expected - extracted
        extra = extracted - expected
        per_offer_rows.append(
            {
                "offer_id": offer_id,
                "source": sources.get(offers[offer_id]["source_id"], {}).get("code", ""),
                "categories": ",".join(sorted(category_by_offer[offer_id])),
                "title": offers[offer_id]["title"],
                "expected_count": len(expected),
                "extracted_count": len(extracted),
                "matched_count": len(matched),
                "missing_count": len(missing),
                "extra_count": len(extra),
                "recall_pct": percentage(len(matched), len(expected)),
                "precision_pct": percentage(len(matched), len(extracted)),
                "expected": ";".join(f"{kind}:{code}" for kind, code in sorted(expected)),
                "extracted": ";".join(f"{kind}:{code}" for kind, code in sorted(extracted)),
                "missing": ";".join(f"{kind}:{code}" for kind, code in sorted(missing)),
                "extra": ";".join(f"{kind}:{code}" for kind, code in sorted(extra)),
            }
        )

    with report_path.open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=list(per_offer_rows[0].keys()))
        writer.writeheader()
        writer.writerows(per_offer_rows)

    level_counts = Counter()
    kind_counts = Counter()
    required_by_kind = Counter()
    for items in criteria_detail_by_offer.values():
        for item in items:
            level_counts[item["level"]] += 1
            kind_counts[item["kind"]] += 1
            if item["is_required"]:
                required_by_kind[item["kind"]] += 1

    coverage = {
        "active_offers": len(active_ids),
        "offers_with_any_extracted_criteria": sum(1 for offer_id in active_ids if criteria_by_offer[offer_id]),
        "offers_with_any_expected_criteria": sum(1 for offer_id in active_ids if expected_by_offer[offer_id]),
        "offers_with_full_expected_extracted": sum(
            1 for offer_id in active_ids if expected_by_offer[offer_id] and expected_by_offer[offer_id] <= criteria_by_offer[offer_id]
        ),
        "offers_with_missing_expected": sum(
            1 for offer_id in active_ids if expected_by_offer[offer_id] - criteria_by_offer[offer_id]
        ),
        "offers_with_extracted_required": sum(
            1 for offer_id in active_ids if any(item["is_required"] for item in criteria_detail_by_offer[offer_id])
        ),
        "offers_with_extracted_technology": sum(
            1 for offer_id in active_ids if any(key[0] == "technology" for key in criteria_by_offer[offer_id])
        ),
        "offers_with_required_technology": sum(
            1 for offer_id in active_ids if any(item["kind"] == "technology" and item["is_required"] for item in criteria_detail_by_offer[offer_id])
        ),
    }

    missing_by_code = Counter(key for _, key in missing_pairs)
    extra_by_code = Counter(key for _, key in extra_pairs)
    worst_offers = sorted(
        [row for row in per_offer_rows if row["expected_count"] > 0],
        key=lambda row: (row["recall_pct"], -row["missing_count"], row["offer_id"]),
    )[:15]

    print(f"REPORT_PATH={report_path}")
    print("SUMMARY")
    print(f"active_offers={len(active_ids)}")
    print(f"orphan_role_links={orphan_role_links}")
    print(f"criterion_rules_in_code={len(criterion_rules)}")
    print(f"expected_pairs={len(all_expected_pairs)}")
    print(f"extracted_pairs={len(all_extracted_pairs)}")
    print(f"matched_pairs={len(true_positive)}")
    print(f"missing_pairs={len(missing_pairs)}")
    print(f"extra_pairs={len(extra_pairs)}")
    print(f"overall_recall_pct={percentage(len(true_positive), len(all_expected_pairs))}")
    print(f"overall_precision_pct={percentage(len(true_positive), len(all_extracted_pairs))}")
    print()
    print("COVERAGE")
    for key, value in coverage.items():
        if key == "active_offers":
            print(f"{key}={value}")
        else:
            print(f"{key}={value};pct={percentage(value, len(active_ids))}")
    print()
    print("BY_KIND")
    for kind, data in by_kind.items():
        print(kind + " " + " ".join(f"{key}={value}" for key, value in data.items()))
    print()
    print("IT_TECH")
    print(f"it_offers={len(it_ids)}")
    print(f"it_expected_technology_pairs={len(it_expected)}")
    print(f"it_extracted_technology_pairs={len(it_extracted)}")
    print(f"it_matched_technology_pairs={len(it_matched)}")
    print(f"it_technology_recall_pct={percentage(len(it_matched), len(it_expected))}")
    print(f"it_technology_precision_pct={percentage(len(it_matched), len(it_extracted))}")
    print(f"it_offers_with_expected_tech={sum(1 for offer_id in it_ids if any(key[0] == 'technology' for key in expected_by_offer[offer_id]))}")
    print(f"it_offers_with_required_tech={sum(1 for offer_id in it_ids if any(item['kind'] == 'technology' and item['is_required'] for item in criteria_detail_by_offer[offer_id]))}")
    print("LEGACY_IT_TECH_IF_ROLE_8_IS_SOFTWARE_DEVELOPER")
    print(f"legacy_it_offers={len(legacy_it_ids)}")
    print(f"legacy_it_expected_technology_pairs={len(legacy_it_expected)}")
    print(f"legacy_it_extracted_technology_pairs={len(legacy_it_extracted)}")
    print(f"legacy_it_matched_technology_pairs={len(legacy_it_matched)}")
    print(f"legacy_it_technology_recall_pct={percentage(len(legacy_it_matched), len(legacy_it_expected))}")
    print(f"legacy_it_technology_precision_pct={percentage(len(legacy_it_matched), len(legacy_it_extracted))}")
    print(f"legacy_it_offers_with_expected_tech={sum(1 for offer_id in legacy_it_ids if any(key[0] == 'technology' for key in expected_by_offer[offer_id]))}")
    print(f"legacy_it_offers_with_required_tech={sum(1 for offer_id in legacy_it_ids if any(item['kind'] == 'technology' and item['is_required'] for item in criteria_detail_by_offer[offer_id]))}")
    print()
    print("LEVEL_COUNTS")
    for key, value in level_counts.most_common():
        print(f"{key}={value}")
    print()
    print("KIND_COUNTS")
    for key, value in kind_counts.most_common():
        print(f"{key}={value};required={required_by_kind[key]}")
    print()
    print("TOP_MISSING")
    for (kind, code), count in missing_by_code.most_common(25):
        print(f"{kind}:{code}={count}")
    print()
    print("TOP_EXTRA")
    for (kind, code), count in extra_by_code.most_common(25):
        print(f"{kind}:{code}={count}")
    print()
    print("WORST_OFFERS")
    for row in worst_offers:
        print(
            f"{row['offer_id']}|{row['source']}|{row['recall_pct']}|"
            f"miss={row['missing_count']}|{row['title'][:100]}|missing={row['missing'][:200]}"
        )


if __name__ == "__main__":
    main()
