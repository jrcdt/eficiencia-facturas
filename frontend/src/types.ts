export type InvoiceStatus = 'Issued' | 'Partial' | 'Cancelled';
export type PaymentStatus = 'Paid' | 'Pending' | 'Overdue';
export interface Invoice {
  number: number;
  issuedOn: string;
  dueOn: string;
  customerName: string;
  customerRun: string;
  totalAmount: number;
  creditTotal: number;
  balance: number;
  invoiceStatus: InvoiceStatus;
  paymentStatus: PaymentStatus;
  daysOverdue: number;
}
export interface Page<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}
export interface CreditNote {
  number: number;
  createdOn: string;
  amount: number;
}
export interface Detail {
  invoice: Invoice;
  customerEmail: string;
  paymentMethod: string | null;
  paidOn: string | null;
  lines: { productName: string; unitPrice: number; quantity: number; subtotal: number }[];
  creditNotes: CreditNote[];
}
export interface Summary {
  activeCount: number;
  inconsistentCount: number;
  overdueWithoutActivityCount: number;
  totalAmount: number;
  creditTotal: number;
  outstandingAmount: number;
  asOf: string;
  paymentSummary: { status: PaymentStatus; count: number; percentage: number; amount: number }[];
}
export interface Inconsistent {
  number: number;
  customerName: string;
  declaredTotal: number;
  productTotal: number;
  difference: number;
}
export interface ImportResult {
  imported: number;
  active: number;
  inconsistent: number;
  skipped: number;
  skippedNumbers: number[];
  inconsistentNumbers: number[];
}
