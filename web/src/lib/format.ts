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
import { i18n, t } from './i18n/index.svelte';

/**
 * What a session is doing, for the chip on the review screen. A saved one says how it was
 * saved — downloaded, or written into the server's folder — which only its history knows.
 */
export function sessionStateLabel(session: SessionDto): string {
	switch (session.State) {
		case SessionState.Scanning:
			return t('home.stateScanning');
		case SessionState.Review:
			return t('home.stateReview');
		case SessionState.Exporting:
			return t('home.statePreparing');
		case SessionState.Done:
			return statusLabels[sessionStatus(session)];
		case SessionState.Failed:
			return t('home.stateFailed');
		default:
			return t('home.stateIdle');
	}
}

export function scannerStateLabel(state: ScannerState | undefined): string {
	switch (state) {
		case ScannerState.Ready:
			return t('home.scannerReady');
		case ScannerState.Busy:
			return t('home.stateScanning');
		case ScannerState.NoDevice:
			return t('home.scannerNoDevice');
		case ScannerState.Error:
			return t('home.scannerError');
		default:
			return t('home.scannerChecking');
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
	const time = date.toLocaleTimeString(i18n.locale, { hour: '2-digit', minute: '2-digit' });
	return sameDay
		? time
		: `${date.toLocaleDateString(i18n.locale, { month: 'short', day: 'numeric' })} ${time}`;
}

export function sourceLabel(source: ScanSource | undefined): string {
	switch (source) {
		case ScanSource.Feeder:
			return t('home.sourceFeeder');
		case ScanSource.Duplex:
			return t('home.sourceDuplex');
		case ScanSource.Flatbed:
			return t('home.sourceFlatbed');
		default:
			return '';
	}
}

export function colorLabel(color: ColorMode | undefined): string {
	switch (color) {
		case ColorMode.Bw:
			return t('home.colorBw');
		case ColorMode.Grayscale:
			return t('home.colorGrayscale');
		case ColorMode.Color:
			return t('home.colorColor');
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
			return t('home.sizeLetter');
		case PageSize.Legal:
			return t('home.sizeLegal');
		default:
			return '';
	}
}

/**
 * What a past scan shows as its status. Derived from the session's history rather
 * than stored: no events means it is still a draft, and a busy state outranks the
 * history because it says what is happening right now.
 */
// `exported` is a scan saved into the server's output folder. `saved` is what the old
// pick-a-folder feature left behind; no scan gets it any more, but one from before still
// reads that way rather than as a draft.
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

// Getters, so a label read in markup follows the language.
export const statusLabels: Record<SessionStatus, string> = {
	get draft() {
		return t('home.statusDraft');
	},
	get scanning() {
		return t('home.stateScanning');
	},
	get exporting() {
		return t('home.statePreparing');
	},
	get exported() {
		return t('home.statusExported');
	},
	get saved() {
		return t('home.statusSaved');
	},
	get downloaded() {
		return t('home.statusDownloaded');
	},
	get discarded() {
		return t('home.statusDiscarded');
	},
	get failed() {
		return t('home.stateFailed');
	}
};

export const exportFormatLabels: Record<ExportFormat, string> = {
	[ExportFormat.Pdf]: 'PDF',
	[ExportFormat.Jpg]: 'JPG',
	[ExportFormat.Png]: 'PNG'
};

/** The export formats as a segmented picker's options: Settings and review offer the same row. */
export const exportFormatOptions = [ExportFormat.Pdf, ExportFormat.Jpg, ExportFormat.Png].map(
	(f) => ({ value: String(f), label: exportFormatLabels[f] })
);

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
