import { useEffect, useId, useState } from "react";
import Modal from "./Modal";

type AdminActionConfirmModalProps = {
  isOpen: boolean;
  title: string;
  consequence: string;
  actionLabel: string;
  errorMessage?: string | null;
  busy?: boolean;
  onClose: () => void;
  onConfirm: (adminKey: string) => Promise<void>;
};

export default function AdminActionConfirmModal({
  isOpen,
  title,
  consequence,
  actionLabel,
  errorMessage = null,
  busy = false,
  onClose,
  onConfirm,
}: AdminActionConfirmModalProps) {
  const [adminKey, setAdminKey] = useState("");
  const adminKeyId = useId();

  useEffect(() => {
    if (!isOpen) setAdminKey("");
  }, [isOpen]);

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title={title}
      size="sm"
      footer={(
        <>
          <button type="button" className="button-big bg-surface-elevated text-foreground border border-border" onClick={onClose} disabled={busy}>
            Otkaži
          </button>
          <button
            type="button"
            className="button-big button-danger"
            onClick={() => void onConfirm(adminKey.trim())}
            disabled={busy || !adminKey.trim()}
          >
            {busy ? "Radim..." : actionLabel}
          </button>
        </>
      )}
    >
      <div className="space-y-4 text-foreground leading-relaxed">
        <p>{consequence}</p>
        <p>Ova serverska izmena zahteva postojeći admin ključ. Ključ se koristi samo za ovu potvrđenu akciju.</p>
        {errorMessage ? <p role="alert" className="text-[var(--error)]">{errorMessage}</p> : null}
        <label className="grid gap-1.5 text-sm font-medium" htmlFor={adminKeyId}>
          Admin ključ
          <input
            id={adminKeyId}
            type="password"
            autoComplete="off"
            value={adminKey}
            onChange={(event) => setAdminKey(event.target.value)}
            className="rounded-lg border border-border bg-surface px-3 py-2 text-foreground"
          />
        </label>
      </div>
    </Modal>
  );
}
