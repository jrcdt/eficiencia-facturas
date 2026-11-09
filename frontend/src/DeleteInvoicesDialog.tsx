import { useState } from 'react';
import type { FormEvent } from 'react';
import { LoaderCircle, Trash2 } from 'lucide-react';
import { api } from './api';
import { ErrorMessage, Modal, number } from './components';

export default function DeleteInvoicesDialog({
  invoiceNumber,
  totalCount,
  onClose,
  onDeleted,
}: {
  invoiceNumber?: number;
  totalCount: number;
  onClose: () => void;
  onDeleted: (deletedCount: number) => void;
}) {
  const all = invoiceNumber === undefined;
  const [confirmation, setConfirmation] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (busy || (all && confirmation !== 'ELIMINAR')) return;
    setBusy(true);
    setError('');
    try {
      if (all) {
        const result = await api<{ deletedCount: number }>('/invoices', {
          method: 'DELETE',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ confirmation }),
        });
        onDeleted(result.deletedCount);
      } else {
        await api(`/invoices/${invoiceNumber}`, { method: 'DELETE' });
        onDeleted(1);
      }
    } catch (error) {
      setError((error as Error).message);
      setBusy(false);
    }
  }

  return (
    <Modal
      title={all ? 'Eliminar todas las facturas' : `Eliminar factura ${number(invoiceNumber!)}`}
      subtitle={
        all
          ? 'Vacía los datos para probar una nueva importación.'
          : 'Revisa la eliminación antes de confirmar.'
      }
      onClose={() => {
        if (!busy) onClose();
      }}
    >
      <form onSubmit={submit}>
        <div className="delete-warning">
          <Trash2 size={22} />
          <div>
            <strong>
              {all
                ? `Se eliminarán las ${totalCount} facturas guardadas.`
                : `Se eliminará la factura ${number(invoiceNumber!)}.`}
            </strong>
            <p>
              {all &&
                'Incluye todas las facturas activas e inconsistentes, aunque haya filtros aplicados. '}
              También se eliminarán sus productos, pagos y notas de crédito. Esta acción no se puede
              deshacer.
            </p>
          </div>
        </div>
        <p className="delete-explanation">
          Después puedes volver a importar el JSON. Las facturas borradas se cargarán nuevamente con
          los datos del archivo.
        </p>
        {all && (
          <label className="confirmation-field">
            Escribe ELIMINAR para confirmar
            <input
              autoFocus
              autoComplete="off"
              spellCheck={false}
              value={confirmation}
              onChange={(event) => setConfirmation(event.target.value)}
              disabled={busy}
            />
          </label>
        )}
        {error && <ErrorMessage message={error} />}
        <div className="modal-actions">
          <button type="button" className="button secondary" disabled={busy} onClick={onClose}>
            Cancelar
          </button>
          <button className="button danger" disabled={busy || (all && confirmation !== 'ELIMINAR')}>
            {busy ? <LoaderCircle size={16} className="spin" /> : <Trash2 size={16} />}
            {busy ? 'Eliminando…' : all ? 'Eliminar todas' : 'Eliminar factura'}
          </button>
        </div>
      </form>
    </Modal>
  );
}
