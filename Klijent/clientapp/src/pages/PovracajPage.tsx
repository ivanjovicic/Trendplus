import React, { useState } from "react";
import PovracajWizard from "../components/povracaj/PovracajWizard";
import { useNavigate } from "react-router-dom";

export default function PovracajPage() {
  const navigate = useNavigate();
  const [showWizard, setShowWizard] = useState(false);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  const handleSuccess = () => {
    setShowWizard(false);
    setSuccessMessage("? Zapisnik o povraćaju uspešno kreiran!");
    setTimeout(() => setSuccessMessage(null), 5000);
  };

  const handleCancel = () => {
    setShowWizard(false);
  };

  return (
    <div style={{ padding: "2rem" }}>
      <div style={{ maxWidth: 1200, margin: "0 auto" }}>
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "1.5rem" }}>
          <h1 style={{ fontSize: "2rem", fontWeight: 700, color: "#1f2937" }}>
            ↩️ Povraćaj robe
          </h1>
          {!showWizard && (
            <button
              className="button-big"
              onClick={() => setShowWizard(true)}
              style={{ background: "#3b82f6", fontSize: "1rem", padding: "0.75rem 1.5rem" }}
            >
              + Novi povraćaj
            </button>
          )}
        </div>

        {successMessage && (
          <div style={{
            background: "#f0fdf4",
            border: "1px solid #a7f3d0",
            color: "#059669",
            padding: "1rem",
            borderRadius: "8px",
            marginBottom: "1.5rem",
            fontSize: "1rem",
            fontWeight: 600
          }}>
            {successMessage}
          </div>
        )}

        {showWizard ? (
          <PovracajWizard onSuccess={handleSuccess} onCancel={handleCancel} />
        ) : (
          <div className="card" style={{ textAlign: "center", padding: "3rem" }}>
            <p style={{ fontSize: "1.125rem", color: "#6b7280", marginBottom: "1rem" }}>
              Nema kreiranih povraćaja
            </p>
            <p style={{ color: "#9ca3af" }}>
              Kliknite na dugme "Novi povraćaj" da kreirate zapisnik o povraćaju robe
            </p>
          </div>
        )}

        {/* TODO: Dodati listu povraćaja */}
      </div>
    </div>
  );
}
