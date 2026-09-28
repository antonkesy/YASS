import type { ExportResult } from '$lib/hub/generated/Yass.Core.Hub';

/**
 * Hands every document of an export to the browser. A split session produces several
 * files, so this is a loop rather than a single link click; the browser will ask before
 * saving more than one, which is the right prompt for what is really several saves.
 */
export function saveToBrowser(result: ExportResult): number {
	let started = 0;
	for (const document of result.Documents) {
		if (!document.DownloadPath) continue;
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

/** Where a save put things: one path, or how many files it wrote. */
export function savedLabel(result: ExportResult): string {
	const paths = result.Documents.map((d) => d.SavedPath).filter((p): p is string => !!p);
	if (paths.length === 1) return paths[0];
	return `${paths.length} documents`;
}
