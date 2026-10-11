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
import {
	documentMetadata,
	metaFor,
	nameTemplate,
	type DocumentMeta,
	type ExportPrefs
} from '$lib/presets';

/** Not in lib.dom, and only present in a secure context (not on plain-http LAN hosts). */
export type SavePickerType = { description: string; accept: Record<string, string[]> };

export type SavePicker = {
	showSaveFilePicker: (options: {
		suggestedName?: string;
		types?: SavePickerType[];
		excludeAcceptAllOption?: boolean;
	}) => Promise<FileSystemFileHandle>;
	showDirectoryPicker: (options?: { mode?: 'readwrite' }) => Promise<FileSystemDirectoryHandle>;
};

export type SaveTarget = { file: FileSystemFileHandle } | { directory: FileSystemDirectoryHandle };

export function savePicker(scope: object = globalThis): SavePicker | null {
	const api = scope as Partial<SavePicker>;
	return typeof api.showSaveFilePicker === 'function' &&
		typeof api.showDirectoryPicker === 'function'
		? (api as SavePicker)
		: null;
}

export function canSave(session: SessionDto | null, busy = false): boolean {
	return (
		!!session &&
		!busy &&
		(session.State === SessionState.Review || session.State === SessionState.Done) &&
		activePageCount(session) > 0
	);
}

export function exportRequest(
	session: SessionDto,
	prefs: ExportPrefs,
	docMeta: Readonly<Record<string, DocumentMeta>>,
	{ name = session.Name, toServer = false }: { name?: string | null; toServer?: boolean } = {}
): ExportRequest {
	return {
		Format: prefs.format,
		Name: name?.trim() || nameTemplate(prefs).trim() || undefined,
		Metadata: documentMetadata(metaFor(prefs, docMeta, session.Id)),
		ToServer: toServer
	};
}

export function cancelled(error: unknown): boolean {
	return error instanceof Error && error.name === 'AbortError';
}

export const fileExtensions: Record<ExportFormat, string> = {
	[ExportFormat.Pdf]: '.pdf',
	[ExportFormat.Jpg]: '.jpg',
	[ExportFormat.Png]: '.png'
};

const mimeTypes: Record<ExportFormat, string> = {
	[ExportFormat.Pdf]: 'application/pdf',
	[ExportFormat.Jpg]: 'image/jpeg',
	[ExportFormat.Png]: 'image/png'
};

/** The one type the browser's save dialog offers, so it starts on the export's format. */
export function savePickerType(format: ExportFormat): SavePickerType {
	const extension = fileExtensions[format];
	return {
		description: extension.slice(1).toUpperCase(),
		accept: { [mimeTypes[format]]: [extension] }
	};
}

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

export function exportBaseName(session: SessionDto, template: string, now = new Date()): string {
	const name = documentName(session, template, now);
	if (name) return safeFileName(name);

	const at = new Date(session.CreatedAt);
	const createdAt = Number.isNaN(at.getTime()) ? now : at;
	return safeFileName(`scan-${createdAt.toISOString().slice(0, 16).replace(/[T:]/g, '-')}`);
}

/**
 * The name a save dialog opens with, without the extension. With the template switched off only a
 * name given by hand is filled in; otherwise empty, rather than a made-up one.
 */
export function dialogName(
	session: SessionDto,
	prefs: Pick<ExportPrefs, 'nameTemplate' | 'useNameTemplate'>,
	now = new Date()
): string {
	if (prefs.useNameTemplate) return exportBaseName(session, prefs.nameTemplate, now);
	const name = documentName(session, '', now);
	return name ? safeFileName(name) : '';
}

export function numberedFileName(base: string, index: number, extension: string): string {
	return index === 0 ? `${base}${extension}` : `${base}-${index + 1}${extension}`;
}

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
			// pipeTo aborts on failure, leaving the file as it was instead of cut short.
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

export function numberedPath(path: string, index: number): string {
	if (index === 0) return path;
	const slash = Math.max(path.lastIndexOf('/'), path.lastIndexOf('\\'));
	const dot = path.lastIndexOf('.');
	return dot > slash
		? `${path.slice(0, dot)}-${index + 1}${path.slice(dot)}`
		: `${path}-${index + 1}`;
}

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
