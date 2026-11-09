import { useRef, useState } from 'react';
import { Check, FileJson, LoaderCircle, Upload, X } from 'lucide-react';
import { api } from './api';
import { ErrorMessage, Modal } from './components';
import type { ImportResult } from './types';

export default function ImportDialog({
  onClose,
  onImported,
}: {
  onClose: () => void;
  onImported: () => void;
}) {
  const input = useRef<HTMLInputElement>(null);
  const [file, setFile] = useState<File | null>(null);
  const [busy, setBusy] = useState(false);
  const [dragging, setDragging] = useState(false);
  const [error, setError] = useState('');
  const [result, setResult] = useState<ImportResult | null>(null);
  function select(value?: File) {
    setError('');
    if (!value) return;
    if (!value.name.toLowerCase().endsWith('.json')) {
      setError('Selecciona un archivo con extensión .json.');
      return;
    }
    if (value.size === 0 || value.size > 10 * 1024 * 1024) {
      setError('El archivo debe tener contenido y pesar hasta 10 MB.');
      return;
    }
    setFile(value);
  }
  async function upload() {
    if (!file || busy) return;
    setBusy(true);
    setError('');
    try {
      const body = new FormData();
      body.append('file', file);
      setResult(await api<ImportResult>('/imports', { method: 'POST', body }));
      onImported();
    } catch (error) {
      setError((error as Error).message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <Modal
      title={result ? 'Importación completada' : 'Importar facturas'}
      subtitle={
        result
          ? 'Los datos ya están disponibles en tu espacio.'
          : 'Carga tus datos y nosotros validamos cada factura.'
      }
      onClose={() => {
        if (!busy) onClose();
      }}
    >
      {result ? (
        <>
          <div className="import-success">
            <span>
              <Check size={28} />
            </span>
            <h3>{result.imported} facturas importadas</h3>
            <p>
              {result.active} consistentes · {result.inconsistent} inconsistentes · {result.skipped}{' '}
              omitidas
            </p>
          </div>
          {result.inconsistent > 0 && (
            <div className="info-box">
              Las facturas {result.inconsistentNumbers.map((n) => `#${n}`).join(', ')} se
              conservaron en el reporte de inconsistencias y se excluyeron de las operaciones
              activas.
            </div>
          )}
          {result.skipped > 0 && (
            <div className="info-box">
              Se omitieron {result.skipped} números que ya existían. Sus datos y notas de crédito se
              conservaron.
            </div>
          )}
          <div className="modal-actions">
            <button className="button primary" onClick={onClose}>
              Ver mis facturas
            </button>
          </div>
        </>
      ) : (
        <>
          <input
            ref={input}
            type="file"
            accept=".json,application/json"
            aria-label="Archivo JSON"
            className="sr-only"
            disabled={busy}
            onChange={(e) => select(e.target.files?.[0])}
          />
          <button
            className={`dropzone ${dragging ? 'dragging' : ''}`}
            disabled={busy}
            onClick={() => input.current?.click()}
            onDragOver={(e) => {
              e.preventDefault();
              setDragging(true);
            }}
            onDragLeave={() => setDragging(false)}
            onDrop={(e) => {
              e.preventDefault();
              setDragging(false);
              select(e.dataTransfer.files[0]);
            }}
          >
            <span className="upload-icon">
              <Upload size={24} />
            </span>
            <strong>Selecciona o arrastra tu archivo</strong>
            <span>JSON · máximo 10 MB</span>
          </button>
          {file && (
            <div className="selected-file">
              <FileJson size={22} />
              <div>
                <strong>{file.name}</strong>
                <small>{(file.size / 1024).toFixed(1)} KB</small>
              </div>
              <button
                className="icon-button"
                aria-label="Quitar archivo"
                disabled={busy}
                onClick={() => setFile(null)}
              >
                <X size={17} />
              </button>
            </div>
          )}
          <div className="info-box">
            Los estados se calculan automáticamente. Las facturas con diferencias de monto se
            guardan para revisión. Los números existentes se omiten.
          </div>
          {error && <ErrorMessage message={error} />}
          <div className="modal-actions">
            <button className="button secondary" disabled={busy} onClick={onClose}>
              Cancelar
            </button>
            <button className="button primary" disabled={!file || busy} onClick={upload}>
              {busy ? <LoaderCircle className="spin" size={16} /> : <Upload size={16} />}
              {busy ? 'Importando…' : 'Importar archivo'}
            </button>
          </div>
        </>
      )}
    </Modal>
  );
}
