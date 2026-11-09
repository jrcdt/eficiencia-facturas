import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import {
  ArrowRight,
  ArrowUpRight,
  BookOpen,
  ChartNoAxesCombined,
  Check,
  ChevronRight,
  CircleAlert,
  FileCheck2,
  FileText,
  Files,
  LayoutDashboard,
  ListFilter,
  RefreshCw,
  ReceiptText,
  Search,
  ShieldCheck,
  Trash2,
  Upload,
  Wallet,
  X,
} from 'lucide-react';
import { api } from './api';
import {
  date,
  Empty,
  ErrorMessage,
  invoiceLabels,
  Loading,
  money,
  number,
  Pagination,
  paymentLabels,
  Status,
} from './components';
import ImportDialog from './ImportDialog';
import InvoiceDialog from './InvoiceDialog';
import DeleteInvoicesDialog from './DeleteInvoicesDialog';
import type { Inconsistent, Invoice, Page, Summary } from './types';

type Section = 'invoices' | 'reports';
type Report = 'summary' | 'overdue' | 'inconsistent';
const pageSize = 8;
const swaggerUrl = `${(import.meta.env.VITE_API_URL ?? '').replace(/\/$/, '')}/swagger`;

export default function App() {
  const [section, setSection] = useState<Section>('invoices');
  const [report, setReport] = useState<Report>('summary');
  const [summary, setSummary] = useState<Summary | null>(null);
  const [data, setData] = useState<Page<Invoice> | null>(null);
  const [inconsistent, setInconsistent] = useState<Page<Inconsistent> | null>(null);
  const [page, setPage] = useState(1);
  const [input, setInput] = useState('');
  const [search, setSearch] = useState('');
  const [invoiceStatus, setInvoiceStatus] = useState('');
  const [paymentStatus, setPaymentStatus] = useState('');
  const [refresh, setRefresh] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [importing, setImporting] = useState(false);
  const [selected, setSelected] = useState<{ number: number; initialAction?: 'credit' } | null>(
    null,
  );
  const [deleting, setDeleting] = useState<{ number?: number } | null>(null);
  const [notice, setNotice] = useState('');
  const hasFilters = search || invoiceStatus || paymentStatus;

  useEffect(() => {
    let current = true;
    setLoading(true);
    setError('');
    const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    if (section === 'invoices') {
      if (search) query.set('number', search);
      if (invoiceStatus) query.set('invoiceStatus', invoiceStatus);
      if (paymentStatus) query.set('paymentStatus', paymentStatus);
    }
    const path = section === 'reports' && report === 'overdue' ? '/reports/overdue' : '/invoices';
    Promise.all([
      api<Summary>('/reports/summary'),
      section === 'reports' && report === 'inconsistent'
        ? api<Page<Inconsistent>>(`/reports/inconsistent?${query}`)
        : api<Page<Invoice>>(`${path}?${query}`),
    ])
      .then(([summary, result]) => {
        if (!current) return;
        setSummary(summary);
        if (section === 'reports' && report === 'inconsistent')
          setInconsistent(result as Page<Inconsistent>);
        else setData(result as Page<Invoice>);
      })
      .catch((error) => {
        if (current) setError(error.message);
      })
      .finally(() => {
        if (current) setLoading(false);
      });
    return () => {
      current = false;
    };
  }, [page, section, report, search, invoiceStatus, paymentStatus, refresh]);

  useEffect(() => {
    if (notice) {
      const id = setTimeout(() => setNotice(''), 5000);
      return () => clearTimeout(id);
    }
  }, [notice]);
  function navigate(value: Section, tab: Report = 'summary') {
    setSection(value);
    setReport(tab);
    setPage(1);
  }
  function clear() {
    setSearch('');
    setInput('');
    setInvoiceStatus('');
    setPaymentStatus('');
    setPage(1);
  }
  function doSearch(event: FormEvent) {
    event.preventDefault();
    setSearch(input.trim());
    setPage(1);
  }
  const overdueCount = summary?.paymentSummary.find((x) => x.status === 'Overdue')?.count ?? 0;
  const totalStored = (summary?.activeCount ?? 0) + (summary?.inconsistentCount ?? 0);

  function invoiceTable(reportMode = false) {
    if (!data?.items.length)
      return (
        <Empty
          title={
            hasFilters && !reportMode
              ? 'No encontramos facturas'
              : reportMode
                ? 'Todo al día por aquí'
                : 'Tu primera factura empieza aquí'
          }
          description={
            hasFilters && !reportMode
              ? 'Prueba con otro número o cambia los filtros.'
              : reportMode
                ? 'No hay facturas con más de 30 días vencidas sin pago ni notas de crédito.'
                : 'Importa el archivo JSON para consultar y administrar tus facturas.'
          }
        />
      );
    return (
      <>
        <div className="table-scroll">
          <table className="invoice-table">
            <thead>
              <tr>
                <th>Factura</th>
                <th>Cliente</th>
                <th>Vencimiento</th>
                <th>Estado factura</th>
                <th>Estado pago</th>
                <th className="right">Total</th>
                <th>Acciones</th>
              </tr>
            </thead>
            <tbody>
              {data.items.map((invoice) => (
                <tr key={invoice.number}>
                  <td>
                    <button
                      className="invoice-link"
                      onClick={() => setSelected({ number: invoice.number })}
                    >
                      {number(invoice.number)}
                    </button>
                    <small>{date(invoice.issuedOn)}</small>
                  </td>
                  <td>
                    <div className="customer-cell">
                      <span className={`avatar tone-${invoice.number % 4}`}>
                        {invoice.customerName
                          .split(' ')
                          .map((n) => n[0])
                          .slice(0, 2)
                          .join('')}
                      </span>
                      <div>
                        <strong>{invoice.customerName}</strong>
                        <small>{invoice.customerRun}</small>
                      </div>
                    </div>
                  </td>
                  <td>
                    <span className="due-date">{date(invoice.dueOn)}</span>
                    {invoice.paymentStatus === 'Overdue' && (
                      <small className="overdue-days">{invoice.daysOverdue} días de atraso</small>
                    )}
                  </td>
                  <td>
                    <Status value={invoice.invoiceStatus} />
                  </td>
                  <td>
                    <Status payment value={invoice.paymentStatus} />
                  </td>
                  <td className="right amount">{money(invoice.totalAmount)}</td>
                  <td>
                    <div className="row-actions">
                      <button
                        className="table-action"
                        aria-label={`Ver factura ${invoice.number}`}
                        onClick={() => setSelected({ number: invoice.number })}
                      >
                        Ver detalle <ArrowUpRight size={13} />
                      </button>
                      <button
                        className="table-action credit-action"
                        aria-label={`Crear nota de crédito para factura ${invoice.number}`}
                        disabled={invoice.balance <= 0}
                        title={
                          invoice.balance <= 0
                            ? 'La factura no tiene saldo disponible para nuevas notas de crédito.'
                            : 'Agregar una nota de crédito'
                        }
                        onClick={() =>
                          setSelected({ number: invoice.number, initialAction: 'credit' })
                        }
                      >
                        <ReceiptText size={13} /> Crear NC
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <Pagination page={page} total={data.totalCount} size={pageSize} onChange={setPage} />
      </>
    );
  }

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <a
          className="brand"
          href="#"
          onClick={(e) => {
            e.preventDefault();
            navigate('invoices');
          }}
        >
          <span className="brand-symbol">
            e<span />
          </span>
          <span>
            eficiencia<span className="brand-dot">.</span>
          </span>
        </a>
        <div className="workspace">
          <span className="workspace-icon">
            <Files size={18} />
          </span>
          <div>
            <strong>Mi espacio de trabajo</strong>
            <small>Administración financiera</small>
          </div>
          <span className="workspace-dot" />
        </div>
        <span className="nav-label">PRINCIPAL</span>
        <nav aria-label="Navegación principal">
          <button
            className={section === 'invoices' ? 'nav-item active' : 'nav-item'}
            onClick={() => navigate('invoices')}
          >
            <FileText size={19} />
            <span>Facturas</span>
            {summary && <span className="nav-count">{summary.activeCount}</span>}
          </button>
          <button
            className={section === 'reports' ? 'nav-item active' : 'nav-item'}
            onClick={() => navigate('reports')}
          >
            <ChartNoAxesCombined size={19} />
            <span>Reportes</span>
          </button>
        </nav>
        <div className="sidebar-bottom">
          <div className="sidebar-tip">
            <span>
              <ShieldCheck size={20} />
            </span>
            <strong>Datos claros. Mejores decisiones.</strong>
            <p>Cada factura validada, cada movimiento en su lugar.</p>
            <button onClick={() => setImporting(true)}>
              Importar mis datos <ArrowRight size={15} />
            </button>
          </div>
          <a className="docs-link" href={swaggerUrl} target="_blank" rel="noreferrer">
            <BookOpen size={17} />
            Documentación API
            <ArrowUpRight size={14} />
          </a>
          <div className="profile">
            <span className="profile-avatar">AD</span>
            <div>
              <strong>Administrador</strong>
              <small>Espacio local</small>
            </div>
            <span className="online-dot" />
          </div>
        </div>
      </aside>

      <div className="main-shell">
        <header className="topbar">
          <div className="breadcrumb">
            <LayoutDashboard size={16} />
            <ChevronRight size={13} />
            <span>Administración</span>
            <ChevronRight size={13} />
            <strong>{section === 'invoices' ? 'Facturas' : 'Reportes'}</strong>
          </div>
          <div className="topbar-right">
            <span className="environment-dot" />
            Entorno local
            <span className="header-divider" />
            <span className="mini-avatar">AD</span>
          </div>
        </header>
        <main>
          <div className="page-heading">
            <div>
              <div className="eyebrow">TU OPERACIÓN, EN ORDEN</div>
              <h1>
                {section === 'invoices' ? 'Gestión de facturas' : 'Reportes y análisis'}
                <span>.</span>
              </h1>
              <p>
                {section === 'invoices'
                  ? 'Una vista clara de tus facturas, pagos y notas de crédito.'
                  : 'Transforma tus datos en decisiones. Revisa el estado de tu operación.'}
              </p>
            </div>
            <div className="heading-actions">
              <button
                className="button danger-outline"
                disabled={loading || totalStored === 0}
                onClick={() => setDeleting({})}
              >
                <Trash2 size={16} /> Eliminar facturas
              </button>
              <button className="button primary" onClick={() => setImporting(true)}>
                <Upload size={17} />
                Importar JSON
              </button>
            </div>
          </div>

          <div className="stats-grid">
            <div className="stat-card">
              <div className="stat-top">
                <span>Facturas activas</span>
                <span className="stat-icon neutral">
                  <Files size={19} />
                </span>
              </div>
              <strong>{summary?.activeCount ?? '—'}</strong>
              <div className="stat-bottom">
                <span className="mini-pill">
                  <Check size={11} />
                  Validadas
                </span>
                <span>listas para gestionar</span>
              </div>
            </div>
            <div className="stat-card">
              <div className="stat-top">
                <span>Monto total facturado</span>
                <span className="stat-icon green">
                  <Wallet size={19} />
                </span>
              </div>
              <strong className="money-stat">{summary ? money(summary.totalAmount) : '—'}</strong>
              <div className="stat-bottom">
                En facturas consistentes <span className="currency">CLP</span>
              </div>
            </div>
            <div className="stat-card">
              <div className="stat-top">
                <span>Facturas vencidas</span>
                <span className="stat-icon orange">
                  <CircleAlert size={19} />
                </span>
              </div>
              <strong>{summary ? overdueCount : '—'}</strong>
              <div className="stat-bottom">
                <span className="status-dot orange-dot" />
                Pendientes de pago
              </div>
            </div>
            <button
              className="stat-card clickable"
              onClick={() => navigate('reports', 'inconsistent')}
            >
              <div className="stat-top">
                <span>Por revisar</span>
                <span className="stat-icon amber">
                  <FileCheck2 size={19} />
                </span>
              </div>
              <strong>{summary?.inconsistentCount ?? '—'}</strong>
              <div className="stat-bottom">
                Facturas inconsistentes <ArrowUpRight size={15} />
              </div>
            </button>
          </div>

          {section === 'invoices' ? (
            <section className="panel">
              <div className="panel-heading">
                <div className="panel-title">
                  <h2>Todas las facturas</h2>
                  <span className="count">{summary?.activeCount ?? 0}</span>
                </div>
                <button
                  className="icon-button"
                  aria-label="Actualizar datos"
                  onClick={() => setRefresh((n) => n + 1)}
                  disabled={loading}
                >
                  <RefreshCw size={16} className={loading ? 'spin' : ''} />
                </button>
              </div>
              <div className="filterbar">
                <form className="search-form" onSubmit={doSearch}>
                  <Search size={17} />
                  <input
                    aria-label="Buscar por número de factura"
                    type="search"
                    inputMode="numeric"
                    pattern="[0-9]*"
                    value={input}
                    onChange={(e) => {
                      setInput(e.target.value);
                      if (!e.target.value) {
                        setSearch('');
                        setPage(1);
                      }
                    }}
                    placeholder="Buscar por número de factura…"
                  />
                  <button type="submit" className="search-submit" aria-label="Buscar">
                    <ArrowRight size={16} />
                  </button>
                </form>
                <div className="filters">
                  <ListFilter size={16} className="filter-icon" />
                  <select
                    aria-label="Estado de factura"
                    value={invoiceStatus}
                    onChange={(e) => {
                      setInvoiceStatus(e.target.value);
                      setPage(1);
                    }}
                  >
                    <option value="">Estado de factura</option>
                    {Object.entries(invoiceLabels).map(([value, label]) => (
                      <option key={value} value={value}>
                        {label}
                      </option>
                    ))}
                  </select>
                  <select
                    aria-label="Estado de pago"
                    value={paymentStatus}
                    onChange={(e) => {
                      setPaymentStatus(e.target.value);
                      setPage(1);
                    }}
                  >
                    <option value="">Estado de pago</option>
                    {Object.entries(paymentLabels).map(([value, label]) => (
                      <option key={value} value={value}>
                        {label}
                      </option>
                    ))}
                  </select>
                </div>
              </div>
              {hasFilters && (
                <div className="filter-summary">
                  <span>Filtros aplicados {search && `· Factura #${search}`}</span>
                  <button className="text-button" onClick={clear}>
                    Limpiar filtros <X size={13} />
                  </button>
                </div>
              )}
              {error ? (
                <ErrorMessage message={error} retry={() => setRefresh((n) => n + 1)} />
              ) : loading ? (
                <Loading />
              ) : (
                invoiceTable()
              )}
            </section>
          ) : (
            <>
              <div className="report-tabs" role="tablist" aria-label="Tipo de reporte">
                {(
                  [
                    ['summary', 'Resumen de pagos'],
                    ['overdue', 'Vencidas +30 días'],
                    ['inconsistent', 'Inconsistencias'],
                  ] as const
                ).map(([value, label]) => (
                  <button
                    role="tab"
                    aria-selected={report === value}
                    className={report === value ? 'selected' : ''}
                    key={value}
                    onClick={() => {
                      setReport(value);
                      setPage(1);
                    }}
                  >
                    {label}
                    {value === 'inconsistent' && <span>{summary?.inconsistentCount ?? 0}</span>}
                  </button>
                ))}
              </div>
              {error ? (
                <ErrorMessage message={error} retry={() => setRefresh((n) => n + 1)} />
              ) : loading ? (
                <section className="panel">
                  <Loading />
                </section>
              ) : report === 'summary' ? (
                <div className="report-grid">
                  <section className="panel payment-panel">
                    <div className="panel-heading">
                      <h2>Distribución por estado de pago</h2>
                      <span className="muted">{summary?.activeCount} facturas</span>
                    </div>
                    <div className="distribution-bar">
                      {summary?.paymentSummary.map((item) => (
                        <div
                          className={item.status.toLowerCase()}
                          key={item.status}
                          style={{ width: `${item.percentage}%` }}
                          title={`${paymentLabels[item.status]}: ${item.percentage}%`}
                        />
                      ))}
                    </div>
                    <div className="summary-table">
                      {summary?.paymentSummary.map((item) => (
                        <div key={item.status}>
                          <Status payment value={item.status} />
                          <span>{item.count} facturas</span>
                          <strong>{item.percentage.toLocaleString('es-CL')}%</strong>
                          <span>{money(item.amount)}</span>
                        </div>
                      ))}
                    </div>
                    <p className="report-footnote">
                      Porcentajes sobre facturas consistentes. El total corresponde al monto
                      declarado, antes de NC.
                    </p>
                  </section>
                  <section className="panel balance-panel">
                    <div className="stat-icon green">
                      <Wallet size={22} />
                    </div>
                    <p>Saldo pendiente de cobro</p>
                    <strong>{summary ? money(summary.outstandingAmount) : '—'}</strong>
                    <small>
                      Saldo después de notas de crédito en facturas sin pago registrado.
                    </small>
                    <div>
                      <span>Notas de crédito emitidas</span>
                      <b>{summary ? money(summary.creditTotal) : '—'}</b>
                    </div>
                  </section>
                  <button
                    className="report-callout"
                    onClick={() => {
                      setReport('overdue');
                      setPage(1);
                    }}
                  >
                    <span className="stat-icon orange">
                      <CircleAlert size={21} />
                    </span>
                    <div>
                      <h3>{summary?.overdueWithoutActivityCount} facturas requieren seguimiento</h3>
                      <p>Con más de 30 días de atraso, sin pago ni notas de crédito.</p>
                    </div>
                    <ArrowRight size={20} />
                  </button>
                </div>
              ) : report === 'overdue' ? (
                <section className="panel">
                  <div className="panel-heading">
                    <div>
                      <h2>Vencidas por más de 30 días</h2>
                      <p>Solo facturas consistentes, sin pago y sin ninguna nota de crédito.</p>
                    </div>
                    <span className="count">{data?.totalCount ?? 0}</span>
                  </div>
                  {invoiceTable(true)}
                </section>
              ) : (
                <section className="panel">
                  <div className="panel-heading">
                    <div>
                      <h2>Facturas inconsistentes</h2>
                      <p>
                        El total declarado no coincide con la suma de los subtotales. Excluidas de
                        la gestión activa.
                      </p>
                    </div>
                    <span className="count warning">{inconsistent?.totalCount ?? 0}</span>
                  </div>
                  {!inconsistent?.items.length ? (
                    <Empty
                      title="Todos los montos coinciden"
                      description="No hay facturas inconsistentes para revisar."
                    />
                  ) : (
                    <>
                      <div className="table-scroll">
                        <table>
                          <thead>
                            <tr>
                              <th>Factura</th>
                              <th>Cliente</th>
                              <th className="right">Total declarado</th>
                              <th className="right">Suma de productos</th>
                              <th className="right">Diferencia</th>
                              <th>Acciones</th>
                            </tr>
                          </thead>
                          <tbody>
                            {inconsistent.items.map((item) => (
                              <tr key={item.number}>
                                <td className="invoice-link">{number(item.number)}</td>
                                <td>{item.customerName}</td>
                                <td className="right amount">{money(item.declaredTotal)}</td>
                                <td className="right amount">{money(item.productTotal)}</td>
                                <td className="right difference">{money(item.difference)}</td>
                                <td>
                                  <button
                                    className="table-action danger-text"
                                    aria-label={`Eliminar factura ${item.number}`}
                                    onClick={() => setDeleting({ number: item.number })}
                                  >
                                    <Trash2 size={13} /> Eliminar
                                  </button>
                                </td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      </div>
                      <Pagination
                        page={page}
                        total={inconsistent.totalCount}
                        size={pageSize}
                        onChange={setPage}
                      />
                    </>
                  )}
                </section>
              )}
            </>
          )}

          <div className="bottom-note">
            <ShieldCheck size={15} />
            <span>
              Los estados se calculan automáticamente a partir de los pagos y notas de crédito.
            </span>
            <span className="as-of">{summary && `Al ${date(summary.asOf)}`}</span>
          </div>
          {section === 'invoices' && (summary?.inconsistentCount ?? 0) > 0 && (
            <button className="review-banner" onClick={() => navigate('reports', 'inconsistent')}>
              <span className="review-icon">
                <CircleAlert size={19} />
              </span>
              <span>
                <strong>
                  Hay {summary?.inconsistentCount} facturas que necesitan una revisión
                </strong>
                <small>
                  Sus montos no coinciden. Se conservaron en el reporte de inconsistencias.
                </small>
              </span>
              <span className="review-link">
                Revisar reporte <ArrowRight size={16} />
              </span>
            </button>
          )}
          <footer>
            <span>
              eficiencia. <span>Menos fricción, más claridad.</span>
            </span>
            <span>
              Gestión de facturas · {summary?.asOf.slice(0, 4) ?? new Date().getFullYear()}
            </span>
          </footer>
        </main>
      </div>
      {importing && (
        <ImportDialog
          onClose={() => setImporting(false)}
          onImported={() => {
            setRefresh((n) => n + 1);
            setPage(1);
          }}
        />
      )}
      {selected !== null && (
        <InvoiceDialog
          invoiceNumber={selected.number}
          initialAction={selected.initialAction}
          onClose={() => setSelected(null)}
          onUpdated={() => {
            setRefresh((n) => n + 1);
            setNotice('Datos actualizados correctamente.');
          }}
          onDelete={() => {
            setDeleting({ number: selected.number });
            setSelected(null);
          }}
        />
      )}
      {deleting && (
        <DeleteInvoicesDialog
          invoiceNumber={deleting.number}
          totalCount={totalStored}
          onClose={() => setDeleting(null)}
          onDeleted={(count) => {
            const all = deleting.number === undefined;
            setDeleting(null);
            setPage(1);
            if (all) {
              clear();
              navigate('invoices');
            }
            setRefresh((n) => n + 1);
            setNotice(
              all
                ? `Se eliminaron ${count} facturas. Ya puedes importar el JSON nuevamente.`
                : 'Factura eliminada correctamente.',
            );
          }}
        />
      )}
      {notice && (
        <div className="toast" role="status">
          <Check size={17} />
          {notice}
          <button aria-label="Cerrar notificación" onClick={() => setNotice('')}>
            <X size={15} />
          </button>
        </div>
      )}
    </div>
  );
}
