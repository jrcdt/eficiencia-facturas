import { useEffect, useRef } from 'react';
import type { ReactNode } from 'react';
import { ChevronLeft, ChevronRight, CircleAlert, FileSearch, LoaderCircle, X } from 'lucide-react';
import type { InvoiceStatus, PaymentStatus } from './types';

export const money = (value: number) =>
  new Intl.NumberFormat('es-CL', {
    style: 'currency',
    currency: 'CLP',
    maximumFractionDigits: 2,
  }).format(value);
export const date = (value: string) =>
  new Intl.DateTimeFormat('es-CL', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
  }).format(new Date(`${value}T12:00:00`));
export const number = (value: number) => `#${String(value).padStart(4, '0')}`;
export const invoiceLabels: Record<InvoiceStatus, string> = {
  Issued: 'Emitida',
  Partial: 'Parcial',
  Cancelled: 'Anulada',
};
export const paymentLabels: Record<PaymentStatus, string> = {
  Paid: 'Pagada',
  Pending: 'Pendiente',
  Overdue: 'Vencida',
};

export function Status({
  value,
  payment = false,
}: {
  value: InvoiceStatus | PaymentStatus;
  payment?: boolean;
}) {
  return (
    <span className={`badge ${value.toLowerCase()}`}>
      <i />
      {payment ? paymentLabels[value as PaymentStatus] : invoiceLabels[value as InvoiceStatus]}
    </span>
  );
}

export function Modal({
  title,
  subtitle,
  children,
  onClose,
  wide = false,
}: {
  title: string;
  subtitle?: string;
  children: ReactNode;
  onClose: () => void;
  wide?: boolean;
}) {
  const ref = useRef<HTMLDialogElement>(null);
  useEffect(() => {
    const dialog = ref.current!;
    dialog.showModal();
    return () => dialog.close();
  }, []);
  return (
    <dialog
      ref={ref}
      className={`modal ${wide ? 'wide' : ''}`}
      onCancel={(event) => {
        event.preventDefault();
        onClose();
      }}
      onClick={(event) => {
        if (event.target === event.currentTarget) {
          const rect = event.currentTarget.getBoundingClientRect();
          if (
            event.clientX < rect.left ||
            event.clientX > rect.right ||
            event.clientY < rect.top ||
            event.clientY > rect.bottom
          )
            onClose();
        }
      }}
      aria-label={title}
    >
      <div className="modal-head">
        <div>
          <h2>{title}</h2>
          {subtitle && <p>{subtitle}</p>}
        </div>
        <button className="icon-button" aria-label="Cerrar" onClick={onClose}>
          <X size={20} />
        </button>
      </div>
      {children}
    </dialog>
  );
}

export function ErrorMessage({ message, retry }: { message: string; retry?: () => void }) {
  return (
    <div className="error-box" role="alert">
      <CircleAlert size={18} />
      <span>{message}</span>
      {retry && (
        <button className="text-button" onClick={retry}>
          Reintentar
        </button>
      )}
    </div>
  );
}
export function Loading() {
  return (
    <div className="empty">
      <LoaderCircle className="spin" size={28} />
      <p>Cargando información…</p>
    </div>
  );
}
export function Empty({ title, description }: { title: string; description: string }) {
  return (
    <div className="empty">
      <span className="empty-icon">
        <FileSearch size={26} />
      </span>
      <h3>{title}</h3>
      <p>{description}</p>
    </div>
  );
}
export function Pagination({
  page,
  total,
  size,
  onChange,
}: {
  page: number;
  total: number;
  size: number;
  onChange: (page: number) => void;
}) {
  const pages = Math.max(1, Math.ceil(total / size));
  return (
    <div className="pagination">
      <span>
        {total === 0
          ? '0 facturas'
          : `Mostrando ${(page - 1) * size + 1}–${Math.min(page * size, total)} de ${total} facturas`}
      </span>
      <div>
        <button
          className="icon-button"
          aria-label="Página anterior"
          disabled={page <= 1}
          onClick={() => onChange(page - 1)}
        >
          <ChevronLeft size={17} />
        </button>
        <span className="page-number">
          {page} <span>/ {pages}</span>
        </span>
        <button
          className="icon-button"
          aria-label="Página siguiente"
          disabled={page >= pages}
          onClick={() => onChange(page + 1)}
        >
          <ChevronRight size={17} />
        </button>
      </div>
    </div>
  );
}
