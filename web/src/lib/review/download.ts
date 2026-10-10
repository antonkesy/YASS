import type { DesktopHost } from '$lib/hub/bridge';
import {
	ExportFormat,
	type ExportRequest,
	type ExportResult,
	type SessionDto
} from '$lib/hub/generated/Yass.Core.Hub';
import { SessionState } from '$lib/hub/generated/Yass.Core.Domain';
import { activePageCount, documentName } from '$lib/format';
import { t } from '$lib/i18n/index.svelte';
import { documentMetadata, metaFor, type DocumentMeta, type ExportPrefs } from '$lib/presets';

/**
 * The File System Access API's two dialogs. lib.dom has the handles they return but not the
 * dialogs themselves, which only exist in a secure context: a box served over plain http on
 * the LAN has no save dialog to offer, so they are reached through `savePicker`, and callers
 * fall back to an ordinary browser download.
 */
export type SavePicker = {
	showSaveFilePicker: (options: { suggestedName?: string }) => Promise<FileSystemFileHandle>;
	showDirectoryPicker: (options?: { mode?: 'readwrite' }) => Promise<FileSystemDirectoryHandle>;
};

/** One document goes into a chosen file; a scan that splits goes into a chosen folder. */
export type SaveTarget = { file: FileSystemFileHandle } | { directory: FileSystemDirectoryHandle };

export function savePicker(scope: object = globalThis): SavePicker | null {
	const api = scope as Partial<SavePicker>;
	return typeof api.showSaveFilePicker === 'function' &&
		typeof api.showDirectoryPicker === 'function'
		? (api as SavePicker)
		: null;
}

/**
 * Whether a scan can be saved right now: it is in review or already saved, has a page left to
 * save, and no save of it is under way (`busy`). The Save buttons and Ctrl/⌘+S both ask this,
 * so the shortcut cannot start what the greyed-out button would not.
 */
export function canSave(session: SessionDto | null, busy = false): boolean {
	return (
		!!session &&
		!busy &&
		(session.State === SessionState.Review || session.State === SessionState.Done) &&
		activePageCount(session) > 0
	);
}

/**
 * What to ask the server for when saving one scan, wherever it goes: the format from Settings,
 * the scan's name (or the template an unnamed one falls back to), and the PDF properties
 * Settings gives every scan with this scan's own on top. One builder, so a bulk download from
 * home writes the same file Save as… on the review screen would.
 *
 * `name` stands in for the session's own when the caller knows better — a rename still on its
 * way to the server.
 */
export function exportRequest(
	session: SessionDto,
	prefs: ExportPrefs,
	docMeta: Readonly<Record<string, DocumentMeta>>,
	{ name = session.Name, toServer = false }: { name?: string | null; toServer?: boolean } = {}
): ExportRequest {
	return {
		Format: prefs.format,
		Name: name?.trim() || prefs.nameTemplate.trim() || undefined,
		Metadata: documentMetadata(metaFor(prefs, docMeta, session.Id)),
		ToServer: toServer
	};
}

/** True when the user closed the save dialog, which is not an error to report. */
export function cancelled(error: unknown): boolean {
	return error instanceof Error && error.name === 'AbortError';
}

export const fileExtensions: Record<ExportFormat, string> = {
	[ExportFormat.Pdf]: '.pdf',
	[ExportFormat.Jpg]: '.jpg',
	[ExportFormat.Png]: '.png'
};

/** Trims a name down to what every filesystem will take, without leaving it empty. */
export function safeFileName(name: string): string {
	const clean = name
		// eslint-disable-next-line no-control-regex
		.replace(/[\u0000-\u001f<>:"/\\|?*]/g, '-')
		.replace(/\s+/g, ' ')
		.replace(/^[\s.]+|[\s.]+$/g, '')
		.slice(0, 120)
		.trim();
	return clean === '' ? 'scan' : clean;
}

/**
 * What the save dialog opens with: the name on the document, or the export template
 * expanded the way the settings sheet previews it, or the date it was scanned. No
 * extension — the caller adds the one its format wants.
 */
export function exportBaseName(session: SessionDto, template: string, now = new Date()): string {
	const name = documentName(session, template, now);
	if (name) return safeFileName(name);

	const at = new Date(session.CreatedAt);
	const createdAt = Number.isNaN(at.getTime()) ? now : at;
	return safeFileName(`scan-${createdAt.toISOString().slice(0, 16).replace(/[T:]/g, '-')}`);
}

/** Names for the documents of one scan, numbered only once there is more than one. */
export function numberedFileName(base: string, index: number, extension: string): string {
	return index === 0 ? `${base}${extension}` : `${base}-${index + 1}${extension}`;
}

/**
 * Streams every document of an export into the place the user picked. The bytes go
 * from the one-shot URL to disk without the page keeping a copy of the document.
 */
export async function saveToDisk(
	result: ExportResult,
	target: SaveTarget,
	base: string,
	extension: string,
	get: typeof fetch = fetch
): Promise<number> {
	const documents = result.Documents;
	if ('file' in target && documents.length > 1) {
		throw new Error(t('review.severalDocuments'));
	}

	let written = 0;
	for (const [index, document] of documents.entries()) {
		const response = await get(document.DownloadPath);
		if (!response.ok) throw new Error(t('review.fetchFailed', { status: response.status }));
		const handle =
			'file' in target
				? target.file
				: await target.directory.getFileHandle(numberedFileName(base, index, extension), {
						create: true
					});
		const writable = await handle.createWritable();
		if (response.body) {
			// Straight through, a chunk at a time. pipeTo closes the file once the last byte is
			// in and aborts it on a failure, which leaves the file as it was instead of cut short.
			await response.body.pipeTo(writable);
		} else {
			try {
				await writable.write(await response.blob());
			} catch (e) {
				await writable.abort(e);
				throw e;
			}
			await writable.close();
		}
		written += 1;
	}
	return written;
}

/**
 * Hands every document of an export to the browser instead, for the browsers with no
 * save dialog to offer. Where it lands is then their download setting, not our choice.
 */
export function saveToBrowser(result: ExportResult): number {
	let started = 0;
	for (const document of result.Documents) {
		const a = window.document.createElement('a');
		a.href = document.DownloadPath;
		a.download = '';
		a.rel = 'noopener';
		window.document.body.append(a);
		a.click();
		a.remove();
		started += 1;
	}
	return started;
}

/** `report.pdf` → `report-2.pdf`: where the second document of a split scan goes beside the first. */
export function numberedPath(path: string, index: number): string {
	if (index === 0) return path;
	const slash = Math.max(path.lastIndexOf('/'), path.lastIndexOf('\\'));
	const dot = path.lastIndexOf('.');
	return dot > slash
		? `${path.slice(0, dot)}-${index + 1}${path.slice(dot)}`
		: `${path}-${index + 1}`;
}

/**
 * The desktop app's save: the host moves each produced file to the chosen file (numbered beside
 * it when the scan split into several) or into the chosen folder. Returns the paths written.
 */
export async function saveNatively(
	result: ExportResult,
	desktop: DesktopHost,
	target: { file: string } | { folder: string },
	base: string,
	extension: string
): Promise<string[]> {
	const saved: string[] = [];
	for (const [index, document] of result.Documents.entries()) {
		const path =
			'file' in target
				? numberedPath(target.file, index)
				: `${target.folder}/${numberedFileName(base, index, extension)}`;
		saved.push(
			await desktop.saveExport(document.DownloadPath, path, 'file' in target && index === 0)
		);
	}
	return saved;
}
