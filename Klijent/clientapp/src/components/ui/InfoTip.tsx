import React from "react";

export default function InfoTip({ text }: { text: string }) {
  return (
    <span className="info-tip" role="note" tabIndex={0} aria-label={text}>
      i
      <span className="info-tip-bubble">{text}</span>
    </span>
  );
}
