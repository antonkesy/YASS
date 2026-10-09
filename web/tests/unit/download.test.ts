import { describe, expect, it, vi } from 'vitest';
import {
	cancelled,
	numberedFileName,
	numberedPath,
	exportBaseName,
	exportFileName,
	safeFileName,
	savePicker,
	saveNatively,
	saveToDisk,
	type SaveTarget
} from '$lib/review/download';
import type { DesktopHost } from '$lib/hub/bridge';
import { ExportFormat, type ExportResult } from '$lib/hub/generated/Yass.Core.Hub';
import { session } from './fixtures';

const result = (...paths: string[]): ExportResult =>
	({
		Documents: paths.map((DownloadPath) => ({ PageCount: 1, DownloadPath }))
	}) as ExportResult;

/** A file handle that keeps what was written to it, standing in for a picked file. */
function fakeFile() {
	const written: Blob[] = [];
	return {
		written,
		handle: {
			createWritable: async () => ({
				write: async (data: Blob) => void written.push(data),
				close: async () => undefined
			})
		}
	};
}

const ok = (body = 'pdf-bytes') =>
	vi.fn(async () => new Response(body, { status: 200 })) as unknown as typeof fetch;

describe('save picker', () => {
	it('is there only when the browser has the whole API', () => {
		expect(savePicker({})).toBeNull();
		expect(savePicker({ showSaveFilePicker: () => undefined })).toBeNull();
		const full = { showSaveFilePicker: () => undefined, showDirectoryPicker: () => undefined };
		expect(savePicker(full)).toBe(full);
	});

	it('tells a closed dialog apart from a real failure', () => {
		const abort = new Error('cancelled');
		abort.name = 'AbortError';
		expect(cancelled(abort)).toBe(true);
		expect(cancelled(new Error('disk full'))).toBe(false);
		expect(cancelled('nope')).toBe(false);
	});
});

describe('file names', () => {
	it('strips what a filesystem will not take, and never ends up empty', () => {
		expect(safeFileName('Tax return 2026')).toBe('Tax return 2026');
		expect(safeFileName('in/voice:2026?')).toBe('in-voice-2026-');
		expect(safeFileName('  ..  ')).toBe('scan');
	});

	it('suggests the document name, then the template, then the date', () => {
		const at = '2026-03-04T15:30:00Z';
		expect(exportBaseName(session('s', { Name: ' Invoice ', CreatedAt: at }), '{YEAR}')).toBe(
			'Invoice'
		);
		expect(exportBaseName(session('s', { CreatedAt: at }), 'scan-{YEAR}-{MONTH}')).toBe(
			'scan-2026-03'
		);
		expect(exportBaseName(session('s', { CreatedAt: at }), '')).toMatch(/^scan-2026-03-04-/);
	});

	it('adds the extension its format wants', () => {
		const named = session('s', { Name: 'Invoice' });
		expect(exportFileName(named, '', ExportFormat.Pdf)).toBe('Invoice.pdf');
		expect(exportFileName(named, '', ExportFormat.Jpg)).toBe('Invoice.jpg');
	});

	it('numbers documents only once there is more than one', () => {
		expect(numberedFileName('Invoice', 0, '.pdf')).toBe('Invoice.pdf');
		expect(numberedFileName('Invoice', 1, '.pdf')).toBe('Invoice-2.pdf');
	});
});

describe('saving to a picked place', () => {
	it('writes one document into the picked file', async () => {
		const file = fakeFile();
		const get = ok();
		const written = await saveToDisk(
			result('/export/s1/tok.pdf'),
			{ file: file.handle } as SaveTarget,
			'Invoice',
			'.pdf',
			get
		);
		expect(written).toBe(1);
		expect(get).toHaveBeenCalledWith('/export/s1/tok.pdf');
		expect(await file.written[0].text()).toBe('pdf-bytes');
	});

	it('writes a split scan into the picked folder, named per document', async () => {
		const files = new Map<string, ReturnType<typeof fakeFile>>();
		const directory = {
			getFileHandle: async (name: string) => {
				const file = fakeFile();
				files.set(name, file);
				return file.handle;
			}
		};
		const written = await saveToDisk(
			result('/export/s1/a.pdf', '/export/s1/b.pdf'),
			{ directory } as SaveTarget,
			'Invoice',
			'.pdf',
			ok()
		);
		expect(written).toBe(2);
		expect([...files.keys()]).toEqual(['Invoice.pdf', 'Invoice-2.pdf']);
	});

	it('refuses to write several documents over one file, and reports a refused fetch', async () => {
		const file = fakeFile();
		await expect(
			saveToDisk(
				result('/export/s1/a.pdf', '/export/s1/b.pdf'),
				{ file: file.handle } as SaveTarget,
				'Invoice',
				'.pdf',
				ok()
			)
		).rejects.toThrow(/folder/);

		const missing = vi.fn(async () => new Response('', { status: 404 })) as unknown as typeof fetch;
		await expect(
			saveToDisk(
				result('/export/s1/a.pdf'),
				{ file: file.handle } as SaveTarget,
				'Invoice',
				'.pdf',
				missing
			)
		).rejects.toThrow(/404/);
	});
});

describe('numberedPath', () => {
	it('numbers before the extension from the second document on', () => {
		expect(numberedPath('/home/me/report.pdf', 0)).toBe('/home/me/report.pdf');
		expect(numberedPath('/home/me/report.pdf', 1)).toBe('/home/me/report-2.pdf');
		expect(numberedPath('C:\\Scans\\report.pdf', 2)).toBe('C:\\Scans\\report-3.pdf');
		expect(numberedPath('/home/my.scans/report', 1)).toBe('/home/my.scans/report-2');
	});
});

describe('saveNatively', () => {
	function host() {
		const calls: [string, string, boolean][] = [];
		const desktop: DesktopHost = {
			pickSavePath: async () => null,
			pickFolder: async () => null,
			saveExport: async (path, target, overwrite) => {
				calls.push([path, target, overwrite]);
				return target;
			}
		};
		return { desktop, calls };
	}

	it('replaces the picked file and numbers further documents beside it without overwriting', async () => {
		const { desktop, calls } = host();
		const saved = await saveNatively(
			result('/export/s/a.pdf', '/export/s/b.pdf'),
			desktop,
			{ file: '/home/me/scan.pdf' },
			'scan',
			'.pdf'
		);
		expect(calls).toEqual([
			['/export/s/a.pdf', '/home/me/scan.pdf', true],
			['/export/s/b.pdf', '/home/me/scan-2.pdf', false]
		]);
		expect(saved).toEqual(['/home/me/scan.pdf', '/home/me/scan-2.pdf']);
	});

	it('names documents into a folder and never overwrites there', async () => {
		const { desktop, calls } = host();
		await saveNatively(
			result('/export/s/a.jpg'),
			desktop,
			{ folder: '/tmp/out' },
			'letter',
			'.jpg'
		);
		expect(calls).toEqual([['/export/s/a.jpg', '/tmp/out/letter.jpg', false]]);
	});
});
