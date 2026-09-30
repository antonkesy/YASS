import {
	ColorMode,
	PageSize,
	ScanSource,
	SessionEventKind,
	SessionState
} from './hub/generated/Yass.Core.Domain';
import { ScannerState } from './hub/generated/Yass.Core.Scanning';
import { ExportFormat, type SessionDto } from './hub/generated/Yass.Core.Hub';
import { expandTemplate } from './template';

export function sessionStateLabel(state: SessionState): string {
	switch (state) {
		case SessionState.Scanning:
			return 'Scanning';
		case SessionState.Review:
			return 'Ready to review';
		case SessionState.Exporting:
			return 'Saving';
		case SessionState.Done:
			return 'Saved';
		case SessionState.Failed:
			return 'Failed';
		default:
			return 'Idle';
	}
}

export function scannerStateLabel(state: ScannerState | undefined): string {
	switch (state) {
		case ScannerState.Ready:
			return 'Ready';
		case ScannerState.Busy:
			return 'Scanning';
		case ScannerState.NoDevice:
			return 'No scanner';
		case ScannerState.Error:
			return 'Scanner error';
		default:
			return 'Checking…';
	}
}

/** Pages that will end up in the PDF. */
export function activePageCount(session: SessionDto): number {
	return session.Pages.filter((p) => !p.Deleted).length;
}

export function formatTime(iso: string, now: Date = new Date()): string {
	const date = new Date(iso);
	if (Number.isNaN(date.getTime())) return iso;
	const sameDay = date.toDateString() === now.toDateString();
	const time = date.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
	return sameDay
		? time
		: `${date.toLocaleDateString(undefined, { month: 'short', day: 'numeric' })} ${time}`;
}

export function sourceLabel(source: ScanSource | undefined): string {
	switch (source) {
		case ScanSource.Feeder:
			return 'Feeder';
		case ScanSource.Duplex:
			return 'Feeder, both sides';
		case ScanSource.Flatbed:
			return 'Flatbed';
		default:
			return '';
	}
}

export function colorLabel(color: ColorMode | undefined): string {
	switch (color) {
		case ColorMode.Bw:
			return 'Black & white';
		case ColorMode.Grayscale:
			return 'Grayscale';
		case ColorMode.Color:
			return 'Color';
		default:
			return '';
	}
}

export function pageSizeLabel(size: PageSize | undefined): string {
	switch (size) {
		case PageSize.A4:
			return 'A4';
		case PageSize.A5:
			return 'A5';
		case PageSize.Letter:
			return 'Letter';
		case PageSize.Legal:
			return 'Legal';
		default:
			return '';
	}
}

/** True when the scan will pull sheets through the feeder rather than use the glass. */
export function usesFeeder(source: ScanSource | undefined): boolean {
	return source === ScanSource.Feeder || source === ScanSource.Duplex;
}

/**
 * What a past scan shows as its status. Derived from the session's history rather
 * than stored: no events means it is still a draft, and a busy state outranks the
 * history because it says what is happening right now.
 */
export type SessionStatus =
	'draft' | 'scanning' | 'exporting' | 'exported' | 'saved' | 'downloaded' | 'discarded' | 'failed';

export function sessionStatus(session: SessionDto): SessionStatus {
	if (session.State === SessionState.Scanning) return 'scanning';
	if (session.State === SessionState.Exporting) return 'exporting';
	if (session.State === SessionState.Failed) return 'failed';

	const last = session.Events.at(-1);
	if (!last) return 'draft';
	switch (last.Kind) {
		case SessionEventKind.Exported:
			return 'exported';
		case SessionEventKind.Saved:
			return 'saved';
		case SessionEventKind.Downloaded:
			return 'downloaded';
		case SessionEventKind.Discarded:
			return 'discarded';
		default:
			return 'draft';
	}
}

export const statusLabels: Record<SessionStatus, string> = {
	draft: 'Draft',
	scanning: 'Scanning',
	exporting: 'Saving',
	exported: 'Exported',
	saved: 'Saved',
	downloaded: 'Downloaded',
	discarded: 'Discarded',
	failed: 'Failed'
};

export const exportFormatLabels: Record<ExportFormat, string> = {
	[ExportFormat.Pdf]: 'PDF',
	[ExportFormat.Jpg]: 'JPG',
	[ExportFormat.Png]: 'PNG'
};

/**
 * What a document is called, wherever it is shown or saved: the name on it, or the file name
 * template from settings expanded for it. Null when there is neither — an unnamed scan and an
 * empty template — which is the only case anything has to stand in for.
 *
 * One definition on purpose. A scan that arrives named by its template reads that way on the
 * review screen, in the list of past scans, and in the file that eventually lands on disk; a
 * rename afterwards moves all three, because all three ask this.
 */
export function documentName(
	session: SessionDto,
	template: string,
	now: Date = new Date()
): string | null {
	const named = session.Name?.trim();
	if (named) return named;

	const pattern = template.trim();
	if (!pattern) return null;

	const at = new Date(session.CreatedAt);
	const expanded = expandTemplate(pattern, Number.isNaN(at.getTime()) ? now : at, {
		preset: 'Scan',
		counter: 1,
		dpi: session.OptionsUsed.Dpi ?? 300,
		color: String(session.OptionsUsed.Color ?? '')
	});
	return expanded.trim() || null;
}

/** What a scan is called, or its timestamp when it is called nothing at all. */
export function sessionTitle(
	session: SessionDto,
	template: string,
	now: Date = new Date()
): string {
	return documentName(session, template, now) ?? formatTime(session.CreatedAt, now);
}

/**
 * The last segment of a folder path — what a person calls the folder. A consume folder
 * reads as `/srv/paperless/consume` on the server and as "consume" in conversation, and a
 * chip has room for the latter. Empty and root paths come back as they are.
 */
export function folderName(path: string): string {
	const trimmed = path.trim().replace(/[/\\]+$/, '');
	const last = trimmed.split(/[/\\]/).pop();
	return last || path.trim();
}

/** Where a session's document ended up, when its last event produced a file. */
export function lastEventPath(session: SessionDto): string | null {
	return session.Events.at(-1)?.Path ?? null;
}

export function formatBytes(bytes: number): string {
	if (bytes <= 0) return '0 kB';
	if (bytes < 1000) return '1 kB';
	const units = ['kB', 'MB', 'GB'];
	let value = bytes / 1000;
	let unit = 0;
	while (value >= 1000 && unit < units.length - 1) {
		value /= 1000;
		unit += 1;
	}
	return `${value < 10 && unit > 0 ? value.toFixed(1) : Math.round(value)} ${units[unit]}`;
}
