import { SessionState } from './hub/generated/Yass.Core.Domain';
import { ScannerState } from './hub/generated/Yass.Core.Scanning';
import type { SessionDto } from './hub/generated/Yass.Core.Hub';

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
