import React from 'react'

interface Props {
  open?: boolean
  onClose?: () => void
  onConfirm?: () => void
}

const TransferReviewModal: React.FC<Props> = ({ open, onClose, onConfirm }) => {
  if (!open) return null
  return (
    <div className="fixed inset-0 flex items-center justify-center">
      <div className="bg-white p-6 rounded-lg shadow-lg w-[680px]">
        <h3 className="text-lg font-semibold">Pregled prenosa</h3>
        <div className="mt-4">(Sadržaj pregleda — stavke, troškovi, napomene)</div>
        <div className="mt-6 flex justify-end gap-3">
          <button className="btn" onClick={onClose}>Zatvori</button>
          <button className="btn btn-primary" onClick={onConfirm}>Potvrdi i kreiraj</button>
        </div>
      </div>
    </div>
  )
}

export default TransferReviewModal
