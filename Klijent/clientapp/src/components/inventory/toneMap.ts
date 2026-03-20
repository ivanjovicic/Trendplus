export const TONE = {
  severity: {
    critical: "border-[#7d2940] bg-[#411520] text-[#ffb4c2]",
    warning: "border-[#7c5822] bg-[#412d11] text-[#ffd590]",
    info: "border-[#30516d] bg-[#102231] text-[#8edbff]",
  },
  urgency: {
    urgent: "border-[#7d2940] bg-[#411520] text-[#ffb4c2]",
    recommended: "border-[#7c5822] bg-[#412d11] text-[#ffd590]",
    optional: "border-[#33405a] bg-[#182131] text-[#dbe6fb]",
  },
  aging: {
    "0-30": "border-[#1f6c49] bg-[#123726] text-[#9ff0c7]",
    "31-60": "border-[#42628a] bg-[#162335] text-[#abd7ff]",
    "61-90": "border-[#7c5822] bg-[#412d11] text-[#ffd590]",
    "90+": "border-[#7d2940] bg-[#411520] text-[#ffb4c2]",
  },
  abc: {
    A: "border-[#5f445e] bg-[#261827] text-[#ffc8fb]",
    B: "border-[#36543f] bg-[#17261d] text-[#aef3bf]",
    C: "border-[#3b4558] bg-[#1f2532] text-[#dbe6fb]",
  },
  stock: {
    critical: "bg-[#4b1622] text-[#ffb4c2] border-[#7d2940]",
    warning: "bg-[#493518] text-[#ffd590] border-[#7c5822]",
    healthy: "bg-[#163829] text-[#9ff0c7] border-[#1f6c49]",
  },
  stockPanel: {
    critical: "from-[#411520] to-[#27141a]",
    warning: "from-[#412d11] to-[#211a12]",
    healthy: "from-[#123726] to-[#111b16]",
  },
  actionType: {
    dopuna: "border-[#28574d] bg-[#102b24] text-[#9ff0c7]",
    transfer: "border-[#30516d] bg-[#102231] text-[#8edbff]",
    markdown: "border-[#7c5822] bg-[#412d11] text-[#ffd590]",
    clearance: "border-[#7d2940] bg-[#411520] text-[#ffb4c2]",
  },
  actionStatus: {
    pending: "border-[#30516d] bg-[#102231] text-[#8edbff]",
    approved: "border-[#28574d] bg-[#102b24] text-[#9ff0c7]",
    deferred: "border-[#4b5670] bg-[#172031] text-[#dbe6fb]",
    closed: "border-[#6b2c38] bg-[#281319] text-[#ffc3cf]",
  },
  priority: {
    critical: "text-[#ffb4c2]",
    high: "text-[#ffd590]",
    medium: "text-[#9fe0ff]",
    low: "text-[#dbe6fb]",
  },
} as const;

export function resolveTone(map: Record<string, string>, key: string | null | undefined, fallback: string) {
  return (key && map[key]) || fallback;
}
