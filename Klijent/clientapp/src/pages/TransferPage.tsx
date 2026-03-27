import React from 'react'
import TransferForm from '../components/transfers/TransferForm'
import TransferItemsTable from '../components/transfers/TransferItemsTable'
import TransferUploadCsv from '../components/transfers/TransferUploadCsv'

const TransferPage: React.FC = () => {
  return (
    <div className="page-shell">
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div className="lg:col-span-2">
          <TransferForm />
        </div>
        <aside className="lg:col-span-1">
          <TransferUploadCsv />
          <TransferItemsTable items={[]} onChange={() => {}} />
        </aside>
      </div>
    </div>
  )
}

export default TransferPage
