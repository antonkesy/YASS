import { describe, expect, it, vi } from 'vitest';
import {
	canSave,
	cancelled,
	numberedFileName,
	numberedPath,
	exportBaseName,
	exportRequest,
	fileExtensions,
	safeFileName,
	savePicker,
	saveNatively,
	saveToDisk,
	type SaveTarget
} from '$lib/review/download';
import type { DesktopHost } from '$lib/hub/bridge';
import { defaultExportPrefs } from '$lib/presets';
import { ExportFormat, type ExportResult } from '$lib/hub/generated/Yass.Core.Hub';
import { SessionState } from '$lib/hub/generated/Yass.Core.Domain';
import { page, session, sessionOf } from './fixtures';

const result = (...paths: string[]): ExportResult =>
	({
		Documents: paths.map((DownloadPath) => ({ PageCount: 1, DownloadPath }))
	}) as ExportResult;

function fakeFile() {
	const written: Uint8Array<ArrayBuffer>[] = [];
	const file = { written, closed: false, aborted: false, text: () => new Blob(written).text() };
	return Object.assign(file, {
		handle: {
			createWritable: async () =>
				new WritableStream<Uint8Array<ArrayBuffer>>({
					write: (chunk) => void written.push(chunk),
					close: () => void (file.closed = true),
					abort: () => void (file.aborted = true)
				})
		} as unknown as FileSystemFileHandle
	});
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

describe('canSave', () => {
	it('allows a scan in review or done with a page left, while nothing is saving it', () => {
		expect(canSave(sessionOf('s', 2))).toBe(true);
		expect(canSave(sessionOf('s', 2, { State: SessionState.Done }))).toBe(true);
		expect(canSave(sessionOf('s', 2), true)).toBe(false);
		expect(canSave(null)).toBe(false);
	});

	it('refuses while scanning or exporting, and with every page deleted', () => {
		expect(canSave(sessionOf('s', 2, { State: SessionState.Scanning }))).toBe(false);
		expect(canSave(sessionOf('s', 2, { State: SessionState.Exporting }))).toBe(false);
		expect(canSave(session('s', { Pages: [page(0, { Deleted: true })] }))).toBe(false);
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
		expect(exportBaseName(named, '') + fileExtensions[ExportFormat.Pdf]).toBe('Invoice.pdf');
		expect(exportBaseName(named, '') + fileExtensions[ExportFormat.Jpg]).toBe('Invoice.jpg');
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
		expect(await file.text()).toBe('pdf-bytes');
		expect(file.closed).toBe(true);
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

	it('abandons the file, rather than keep half a document, when the download breaks off', async () => {
		const file = fakeFile();
		const body = new ReadableStream<Uint8Array>({
			start(controller) {
				controller.enqueue(new TextEncoder().encode('%PDF-'));
				controller.error(new Error('connection reset'));
			}
		});
		const broken = vi.fn(
			async () => new Response(body, { status: 200 })
		) as unknown as typeof fetch;
		await expect(
			saveToDisk(result('/export/s1/a.pdf'), { file: file.handle }, 'Invoice', '.pdf', broken)
		).rejects.toThrow(/reset/);
		expect(file.aborted).toBe(true);
		expect(file.closed).toBe(false);
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
			openUrl: async () => {},
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

describe('exportRequest', () => {
	const prefs = { ...defaultExportPrefs, author: 'Ada', nameTemplate: '{YEAR}' };

	it('names the scan, else leaves the template to the server, and stamps its own properties', () => {
		const named = session('s1', { Name: ' Invoice ' });
		expect(exportRequest(named, prefs, { s1: { subject: 'Tax' } })).toEqual({
			Format: ExportFormat.Pdf,
			Name: 'Invoice',
			Metadata: { Author: 'Ada', Subject: 'Tax', Keywords: undefined },
			ToServer: false
		});
		expect(exportRequest(session('s2'), prefs, { s1: { subject: 'Tax' } })).toMatchObject({
			Name: '{YEAR}',
			Metadata: { Author: 'Ada', Subject: undefined }
		});
	});

	it('takes a name and a destination from the caller', () => {
		const request = exportRequest(
			session('s1', { Name: 'Old' }),
			defaultExportPrefs,
			{},
			{
				name: 'New',
				toServer: true
			}
		);
		expect(request).toEqual({
			Format: ExportFormat.Pdf,
			Name: 'New',
			Metadata: undefined,
			ToServer: true
		});
	});
});
