import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import { ArrowDownLeft, Check, CreditCard, LoaderCircle, Plus, Trash2 } from 'lucide-react';
import { api, post } from './api';
import { date, ErrorMessage, Loading, Modal, money, number, Status } from './components';
import type { Detail } from './types';

export default function InvoiceDialog({
  invoiceNumber,
  initialAction,
  onClose,
  onUpdated,
  onDelete,
}: {
  invoiceNumber: number;
  initialAction?: 'credit';
  onClose: () => void;
  onUpdated: () => void;
  onDelete: () => void;
}) {
  const [detail, setDetail] = useState<Detail | null>(null);
  const [error, setError] = useState('');
  const [mode, setMode] = useState<'credit' | 'payment' | null>(initialAction ?? null);
  const [amount, setAmount] = useState('');
  const [method, setMethod] = useState('Transferencia bancaria');
  const [busy, setBusy] = useState(false);
  const [success, setSuccess] = useState('');
  const [reload, setReload] = useState(0);
  useEffect(() => {
    let current = true;
    api<Detail>(`/invoices/${invoiceNumber}`)
      .then((value) => {
        if (current) setDetail(value);
      })
      .catch((error) => {
        if (current) setError(error.message);
      });
    return () => {
      current = false;
    };
  }, [invoiceNumber, reload]);

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (!detail || busy) return;
    setError('');
    setSuccess('');
    const value = Number(amount);
    if (
      mode === 'credit' &&
      (!/^\d+(?:\.\d{1,2})?$/.test(amount) ||
        !Number.isFinite(value) ||
        value <= 0 ||
        value > detail.invoice.balance)
    ) {
      setError(
        'Ingresa un monto mayor a cero, con hasta dos decimales y que no supere el saldo disponible.',
      );
      return;
    }
    if (mode === 'payment' && !method.trim()) {
      setError('Selecciona o ingresa un medio de pago.');
      return;
    }
    setBusy(true);
    try {
      await post(
        `/invoices/${invoiceNumber}/${mode === 'credit' ? 'credit-notes' : 'payments'}`,
        mode === 'credit' ? { amount: value } : { method },
      );
      setSuccess(
        mode === 'credit'
          ? 'Nota de crédito creada correctamente.'
          : 'Pago registrado correctamente.',
      );
      setMode(null);
      setAmount('');
      onUpdated();
      setReload((n) => n + 1);
    } catch (error) {
      setError((error as Error).message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <Modal
      title={`Factura ${number(invoiceNumber)}`}
      subtitle="Detalle y movimientos de la factura"
      wide
      onClose={() => {
        if (!busy) onClose();
      }}
    >
      {error && (
        <ErrorMessage
          message={error}
          retry={
            !detail
              ? () => {
                  setError('');
                  setReload((n) => n + 1);
                }
              : undefined
          }
        />
      )}
      {success && (
        <div className="success-box" role="status">
          <Check size={17} />
          {success}
        </div>
      )}
      {!detail ? (
        !error && <Loading />
      ) : (
        <>
          <div className="detail-customer">
            <div className="avatar large">
              {detail.invoice.customerName
                .split(' ')
                .map((s) => s[0])
                .slice(0, 2)
                .join('')}
            </div>
            <div>
              <h3>{detail.invoice.customerName}</h3>
              <p>
                {detail.invoice.customerRun} · {detail.customerEmail}
              </p>
            </div>
          </div>
          <div className="detail-meta">
            <div>
              <span>Emisión</span>
              <strong>{date(detail.invoice.issuedOn)}</strong>
            </div>
            <div>
              <span>Vencimiento</span>
              <strong>{date(detail.invoice.dueOn)}</strong>
            </div>
            <div>
              <span>Factura</span>
              <Status value={detail.invoice.invoiceStatus} />
            </div>
            <div>
              <span>Pago</span>
              <Status payment value={detail.invoice.paymentStatus} />
            </div>
          </div>
          <div className="table-scroll">
            <table className="detail-table">
              <thead>
                <tr>
                  <th>Producto</th>
                  <th className="right">Cantidad</th>
                  <th className="right">Precio unit.</th>
                  <th className="right">Subtotal</th>
                </tr>
              </thead>
              <tbody>
                {detail.lines.map((line, i) => (
                  <tr key={i}>
                    <td>{line.productName}</td>
                    <td className="right">{line.quantity}</td>
                    <td className="right">{money(line.unitPrice)}</td>
                    <td className="right">{money(line.subtotal)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="detail-totals">
            <div>
              <span>Total factura</span>
              <strong>{money(detail.invoice.totalAmount)}</strong>
            </div>
            <div>
              <span>Notas de crédito</span>
              <strong>−{money(detail.invoice.creditTotal)}</strong>
            </div>
            <div className="balance">
              <span>Saldo después de NC</span>
              <strong>{money(detail.invoice.balance)}</strong>
            </div>
          </div>
          {detail.paidOn && (
            <div className="success-box">
              <CreditCard size={18} />
              <span>
                Pago registrado el {date(detail.paidOn)} · {detail.paymentMethod}
              </span>
            </div>
          )}
          <div className="section-title">
            <h3>
              Notas de crédito <span className="count">{detail.creditNotes.length}</span>
            </h3>
            {detail.invoice.balance > 0 && !mode && (
              <button
                className="text-button"
                onClick={() => {
                  setMode('credit');
                  setError('');
                  setSuccess('');
                }}
              >
                <Plus size={16} />
                Agregar nota de crédito
              </button>
            )}
          </div>
          {detail.creditNotes.length === 0 ? (
            <p className="muted-note">Esta factura aún no tiene notas de crédito.</p>
          ) : (
            <div className="credit-list">
              {detail.creditNotes.map((note) => (
                <div key={note.number}>
                  <span className="credit-icon">
                    <ArrowDownLeft size={18} />
                  </span>
                  <div>
                    <strong>NC {number(note.number)}</strong>
                    <small>{date(note.createdOn)}</small>
                  </div>
                  <strong>−{money(note.amount)}</strong>
                </div>
              ))}
            </div>
          )}
          {mode && (
            <form className="action-form" onSubmit={submit}>
              <h3>{mode === 'credit' ? 'Nueva nota de crédito' : 'Registrar pago'}</h3>
              <p>
                {mode === 'credit'
                  ? `Disponible: ${money(detail.invoice.balance)}. La fecha se asigna automáticamente.`
                  : `Se registrará el pago completo del saldo de ${money(detail.invoice.balance)} con fecha de hoy.`}
              </p>
              {mode === 'credit' ? (
                <label>
                  Monto de la nota de crédito
                  <input
                    autoFocus
                    type="number"
                    min="0.01"
                    max={detail.invoice.balance}
                    step="0.01"
                    required
                    value={amount}
                    onChange={(e) => setAmount(e.target.value)}
                    placeholder="0"
                    disabled={busy}
                  />
                </label>
              ) : (
                <label>
                  Medio de pago
                  <select
                    value={method}
                    onChange={(e) => setMethod(e.target.value)}
                    disabled={busy}
                  >
                    <option>Transferencia bancaria</option>
                    <option>Tarjeta de crédito</option>
                    <option>Tarjeta de débito</option>
                    <option>Efectivo</option>
                    <option>Cheque</option>
                  </select>
                </label>
              )}
              <div className="modal-actions">
                <button
                  type="button"
                  className="button secondary"
                  disabled={busy}
                  onClick={() => {
                    setMode(null);
                    setError('');
                  }}
                >
                  Cancelar
                </button>
                <button className="button primary" disabled={busy}>
                  {busy && <LoaderCircle size={16} className="spin" />}
                  {mode === 'credit' ? 'Crear nota de crédito' : 'Confirmar pago'}
                </button>
              </div>
            </form>
          )}
          <div className="modal-actions bottom-actions">
            <button className="button danger-outline delete-one" disabled={busy} onClick={onDelete}>
              <Trash2 size={16} />
              Eliminar factura
            </button>
            {!detail.paidOn && detail.invoice.balance > 0 && !mode && (
              <button
                className="button primary"
                onClick={() => {
                  setMode('payment');
                  setError('');
                  setSuccess('');
                }}
              >
                <CreditCard size={16} />
                Registrar pago
              </button>
            )}
            <button className="button secondary" disabled={busy} onClick={onClose}>
              Cerrar detalle
            </button>
          </div>
        </>
      )}
    </Modal>
  );
}
