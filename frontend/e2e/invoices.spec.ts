import { test, expect } from '@playwright/test';
import path from 'node:path';

test('importar, filtrar, crear NC, registrar pago y revisar reportes en escritorio y móvil', async ({
  page,
}) => {
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await page.setViewportSize({ width: 1440, height: 1100 });
  await page.goto('/');
  await expect(page.getByText('Tu primera factura empieza aquí')).toBeVisible();
  await page.getByRole('button', { name: 'Importar JSON', exact: true }).click();
  await page
    .getByLabel('Archivo JSON')
    .setInputFiles(path.resolve('../data/bd_exam_invoices.json'));
  await page.getByRole('button', { name: 'Importar archivo', exact: true }).click();
  await expect(page.getByText('50 facturas importadas')).toBeVisible();
  await expect(page.getByText('48 consistentes · 2 inconsistentes · 0 omitidas')).toBeVisible();
  await page.getByRole('button', { name: 'Ver mis facturas' }).click();
  await expect(page.getByText('Mostrando 1–8 de 48 facturas')).toBeVisible();
  await page.screenshot({ path: '../artifacts/desktop.png', fullPage: true });

  await page.getByLabel('Estado de factura', { exact: true }).selectOption('Partial');
  await expect(page.locator('tbody tr')).toHaveCount(6);
  await page.getByRole('button', { name: 'Limpiar filtros' }).click();
  await page.getByLabel('Buscar por número de factura').fill('1');
  await page.getByLabel('Buscar por número de factura').press('Enter');
  await expect(page.locator('tbody tr')).toHaveCount(1);
  await page.getByRole('button', { name: 'Ver factura 1', exact: true }).click();
  const dialog = page.getByRole('dialog');
  await expect(dialog.getByRole('heading', { name: 'Juanita Hugh' })).toBeVisible();
  await expect(
    dialog.getByRole('button', { name: 'Agregar nota de crédito', exact: true }),
  ).toBeVisible();
  await dialog.getByRole('button', { name: 'Cerrar detalle', exact: true }).click();
  await page
    .getByRole('button', { name: 'Crear nota de crédito para factura 1', exact: true })
    .click();
  await expect(
    dialog.getByRole('heading', { name: 'Nueva nota de crédito', exact: true }),
  ).toBeVisible();
  await dialog.getByLabel('Monto de la nota de crédito').fill('265020');
  await dialog.getByRole('button', { name: 'Crear nota de crédito', exact: true }).click();
  await expect(dialog.getByLabel('Monto de la nota de crédito')).toBeVisible();
  await dialog.getByLabel('Monto de la nota de crédito').fill('1000');
  await dialog.getByRole('button', { name: 'Crear nota de crédito', exact: true }).click();
  await expect(dialog.getByText('Nota de crédito creada correctamente.')).toBeVisible();
  await expect(dialog.locator('.badge.partial')).toBeVisible();
  await page.screenshot({ path: '../artifacts/invoice-detail.png', fullPage: true });
  await dialog.getByRole('button', { name: 'Registrar pago', exact: true }).click();
  await dialog.getByRole('button', { name: 'Confirmar pago', exact: true }).click();
  await expect(dialog.getByText('Pago registrado correctamente.')).toBeVisible();
  await expect(dialog.locator('.badge.paid')).toBeVisible();
  await dialog.getByRole('button', { name: 'Cerrar detalle' }).click();

  await page.getByRole('button', { name: 'Reportes', exact: true }).click();
  await expect(
    page.getByRole('heading', { name: 'Distribución por estado de pago' }),
  ).toBeVisible();
  await page.screenshot({ path: '../artifacts/reports.png', fullPage: true });
  await page.getByRole('tab', { name: 'Inconsistencias' }).click();
  await expect(page.locator('tbody tr')).toHaveCount(2);
  await expect(page.locator('tbody')).toContainText('#0041');
  await expect(page.locator('tbody')).toContainText('#0048');
  await page.getByRole('tab', { name: 'Vencidas +30 días' }).click();
  await expect(page.getByRole('heading', { name: 'Vencidas por más de 30 días' })).toBeVisible();
  await expect(page.locator('tbody tr').first()).toBeVisible();

  await page.setViewportSize({ width: 390, height: 844 });
  await page.getByRole('button', { name: 'Facturas', exact: false }).first().click();
  await page.getByRole('button', { name: 'Limpiar filtros' }).click();
  await expect(page.getByText('Mostrando 1–8 de 48 facturas')).toBeVisible();
  const closeNotice = page.getByRole('button', { name: 'Cerrar notificación' });
  if (await closeNotice.isVisible()) await closeNotice.click();
  await page.screenshot({ path: '../artifacts/mobile.png', fullPage: true });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(
    true,
  );
  await page.getByRole('button', { name: 'Importar JSON', exact: true }).click();
  await expect(page.getByRole('dialog')).toBeVisible();
  await page
    .getByLabel('Archivo JSON')
    .setInputFiles(path.resolve('../data/bd_exam_invoices.json'));
  await page.getByRole('button', { name: 'Importar archivo', exact: true }).click();
  await expect(page.getByText('0 facturas importadas')).toBeVisible();
  await expect(page.getByText('0 consistentes · 0 inconsistentes · 50 omitidas')).toBeVisible();
  await page.getByRole('button', { name: 'Ver mis facturas' }).click();

  // Deletion can be cancelled. Confirming one invoice permits importing just that number again.
  await page.getByLabel('Buscar por número de factura').fill('1');
  await page.getByLabel('Buscar por número de factura').press('Enter');
  await page.getByRole('button', { name: 'Ver factura 1', exact: true }).click();
  await dialog.getByRole('button', { name: 'Eliminar factura', exact: true }).click();
  await expect(dialog).toHaveAccessibleName('Eliminar factura #0001');
  await dialog.getByRole('button', { name: 'Cancelar', exact: true }).click();
  await expect(page.locator('tbody tr')).toHaveCount(1);
  await page.getByRole('button', { name: 'Ver factura 1', exact: true }).click();
  await dialog.getByRole('button', { name: 'Eliminar factura', exact: true }).click();
  await dialog.getByRole('button', { name: 'Eliminar factura', exact: true }).click();
  await expect(page.getByText('No encontramos facturas', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Importar JSON', exact: true }).click();
  await page
    .getByLabel('Archivo JSON')
    .setInputFiles(path.resolve('../data/bd_exam_invoices.json'));
  await page.getByRole('button', { name: 'Importar archivo', exact: true }).click();
  await expect(page.getByText('1 facturas importadas', { exact: true })).toBeVisible();
  await expect(page.getByText('1 consistentes · 0 inconsistentes · 49 omitidas')).toBeVisible();
  await page.getByRole('button', { name: 'Ver mis facturas' }).click();

  // "All" applies to all stored rows, even with a search filter and hidden inconsistent invoices.
  await page.getByRole('button', { name: 'Eliminar facturas', exact: true }).click();
  await expect(
    dialog.getByText('Se eliminarán las 50 facturas guardadas.', { exact: true }),
  ).toBeVisible();
  await expect(dialog.getByRole('button', { name: 'Eliminar todas', exact: true })).toBeDisabled();
  await dialog.getByLabel('Escribe ELIMINAR para confirmar').fill('eliminar');
  await expect(dialog.getByRole('button', { name: 'Eliminar todas', exact: true })).toBeDisabled();
  await dialog.getByRole('button', { name: 'Cancelar', exact: true }).click();
  await expect(page.locator('tbody tr')).toHaveCount(1);
  await page.getByRole('button', { name: 'Eliminar facturas', exact: true }).click();
  await dialog.getByLabel('Escribe ELIMINAR para confirmar').fill('ELIMINAR');
  await page.screenshot({ path: '../artifacts/delete-confirmation.png', fullPage: true });
  await dialog.getByRole('button', { name: 'Eliminar todas', exact: true }).click();
  await expect(page.getByText('Tu primera factura empieza aquí', { exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Eliminar facturas', exact: true })).toBeDisabled();
  await page.getByRole('button', { name: 'Importar JSON', exact: true }).click();
  await page
    .getByLabel('Archivo JSON')
    .setInputFiles(path.resolve('../data/bd_exam_invoices.json'));
  await page.getByRole('button', { name: 'Importar archivo', exact: true }).click();
  await expect(page.getByText('50 facturas importadas', { exact: true })).toBeVisible();
  await expect(page.getByText('48 consistentes · 2 inconsistentes · 0 omitidas')).toBeVisible();
  await page.getByRole('button', { name: 'Ver mis facturas' }).click();
  await expect(page.getByText('Mostrando 1–8 de 48 facturas')).toBeVisible();
  expect(errors).toEqual([]);
});
