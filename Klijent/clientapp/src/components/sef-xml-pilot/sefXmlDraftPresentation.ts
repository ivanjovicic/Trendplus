import type { SefXmlEnumValue } from "../../services/sefXmlPilotApi";

export interface SefXmlMatchPresentation {
  label: string;
  exportSource: string;
  exactCodeNeedsReview: boolean;
}

export function isValidPhysicalQuantity(value: string): boolean {
  return /^\d+(?:[.,]\d+)?$/u.test(value.trim());
}

export function getSefXmlMatchPresentation(status: SefXmlEnumValue): SefXmlMatchPresentation {
  const token = String(status);
  switch (token) {
    case "0":
    case "ExactIdMatch":
      return {
        label: "Podudaranje šifre — proveriti",
        exportSource: "Automatsko poređenje šifre — proveriti",
        exactCodeNeedsReview: true,
      };
    case "1":
    case "ExplicitManualMatch":
      return {
        label: "Ručno odabrano",
        exportSource: "Ručni izbor operatera",
        exactCodeNeedsReview: false,
      };
    case "2":
    case "CandidateReview":
      return { label: "Kandidat za proveru", exportSource: "Nije pouzdano potvrđeno", exactCodeNeedsReview: false };
    case "3":
    case "Unmatched":
      return { label: "Nije pronađen kandidat", exportSource: "Nije pouzdano potvrđeno", exactCodeNeedsReview: false };
    case "4":
    case "Ambiguous":
      return { label: "Više mogućih kandidata", exportSource: "Nije pouzdano potvrđeno", exactCodeNeedsReview: false };
    case "5":
    case "Invalid":
      return { label: "Stavka zahteva pregled", exportSource: "Nije pouzdano potvrđeno", exactCodeNeedsReview: false };
    default:
      return { label: "Status zahteva proveru", exportSource: "Nije pouzdano potvrđeno", exactCodeNeedsReview: false };
  }
}
