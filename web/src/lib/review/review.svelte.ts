import { get } from 'svelte/store';
import { goto } from '$app/navigation';
import { resolve } from '$app/paths';
import { getDesktop, getHub } from '$lib/hub/hub';
import { hubError } from '$lib/hub/errors';
import type { PageEdit } from '$lib/hub/generated/Yass.Core.Domain';
import { SessionState } from '$lib/hub/generated/Yass.Core.Domain';
import type { SessionDto } from '$lib/hub/generated/Yass.Core.Hub';
import { t } from '$lib/i18n/index.svelte';
import { cameFrom } from '$lib/navigation';
import { documentMeta, exportPrefs } from '$lib/presets';
import { toast } from '$lib/toast';
import { applyOptimistic, setCropEdit } from './edits';
import {
	cancelled,
	exportBaseName,
	exportRequest,
	fileExtensions,
	savePicker,
	saveNatively,
	saveToBrowser,
	saveToDisk,
	type SaveTarget
} from './download';

/**
 * What the review screen does to its scan, and the state those actions leave behind: the
 * error line, whether a save or a stop is under way, the name a rename is still carrying.
 * Created once per review screen, while it is mounting, since it keeps effects of its own.
 */
export class Review {
	error = $state<string | null>(null);
	exporting = $state(false);
	stopping = $state(false);
	autocropping = $state(false);
	/** Pages from another scan are on their way in; the button that adds them waits. */
	mergeBusy = $state(false);
	/**
	 * The name this browser last asked for, held until the session comes back carrying it.
	 * Hub calls run one at a time per connection, so a rename can sit behind a page read —
	 * and Save as… must not name the file from a session that has not caught up yet.
	 */
	renamedTo = $state<string | null>(null);

	readonly #client = getHub();
	readonly #desktop = getDesktop();
	readonly #id: () => string;
	readonly #session: () => SessionDto | null;

	constructor(id: () => string, session: () => SessionDto | null) {
		this.#id = id;
		this.#session = session;

		// Held until the session comes back carrying it: the call returning is not the same as
		// the session this page reads from having caught up, and dropping it in between would
		// leave Save as… naming the file from a session that still thinks it is unnamed.
		$effect(() => {
			if (this.renamedTo !== null && (this.#session()?.Name ?? '') === this.renamedTo)
				this.renamedTo = null;
		});

		// The SessionChanged that follows a stop is what takes the stop button away.
		$effect(() => {
			if (this.stopping && this.#session()?.State !== SessionState.Scanning) {
				this.stopping = false;
				toast.show(t('review.scanStopped'));
			}
		});
	}

	/** The scan's name as this browser last set it, whether or not the session has caught up. */
	get storedName() {
		return this.renamedTo ?? this.#session()?.Name ?? null;
	}

	/** Runs a hub call, showing what went wrong in the error line instead of throwing. */
	async #attempt(call: () => Promise<unknown>) {
		this.error = null;
		try {
			await call();
		} catch (e) {
			this.error = hubError(e);
		}
	}

	async send(edit: PageEdit) {
		const id = this.#id();
		if (!this.#session()) return;
		this.error = null;
		this.#client.updateLocal(id, (s) => applyOptimistic(s, edit));
		try {
			await this.#client.hub.applyEdits(id, [edit]);
		} catch (e) {
			this.error = hubError(e);
			await this.#client.refresh(id);
		}
	}

	/** Discard throws the scan away entirely: it leaves no row behind in past scans. */
	async discard() {
		const id = this.#id();
		try {
			await this.#client.hub.deleteSession(id);
			cameFrom.leaving(id);
			await goto(resolve('/'));
			toast.show(t('review.scanDiscarded'));
		} catch (e) {
			this.error = hubError(e);
		}
	}

	async rename(name: string) {
		this.renamedTo = name.trim();
		try {
			await this.#client.hub.renameSession(this.#id(), name);
		} catch (e) {
			this.renamedTo = null;
			this.error = hubError(e);
		}
	}

	/**
	 * What every save of this scan asks the server for. The name is the one this browser asked
	 * for, which a rename still in the air behind a page read has not reached the session with.
	 */
	#request(of: SessionDto, toServer: boolean) {
		return exportRequest(of, get(exportPrefs), get(documentMeta), {
			name: this.storedName,
			toServer
		});
	}

	/**
	 * Save as…: asks where the file goes first, while the click is still a user gesture —
	 * the export itself takes long enough that a browser would refuse to open a dialog
	 * afterwards — then streams the finished document straight there. Browsers without
	 * the File System Access API, which needs a secure context, get a plain download.
	 */
	async saveAs() {
		const session = this.#session();
		if (!session) return;
		const prefs = get(exportPrefs);
		const picker = savePicker();
		let target: SaveTarget | null = null;
		const base = exportBaseName(
			{ ...session, Name: this.storedName ?? undefined },
			prefs.nameTemplate
		);
		const extension = fileExtensions[prefs.format];

		if (this.#desktop) {
			await this.#saveAsOnDesktop(session, base, extension);
			return;
		}

		if (picker) {
			try {
				// One scan is one document, so this is always a file rather than a folder.
				target = { file: await picker.showSaveFilePicker({ suggestedName: base + extension }) };
			} catch (e) {
				if (cancelled(e)) return;
				// The dialog exists but would not open: the plain download still gets the file out.
				target = null;
			}
		}

		this.error = null;
		this.exporting = true;
		try {
			const result = await this.#client.hub.export(this.#id(), this.#request(session, false));
			const count = target
				? await saveToDisk(result, target, base, extension)
				: saveToBrowser(result);
			toast.show(t(target ? 'review.savedFiles' : 'review.downloadedFiles', { n: count }));
		} catch (e) {
			this.error = hubError(e);
		} finally {
			this.exporting = false;
		}
	}

	/**
	 * The desktop app's Save as…: the native dialog picks the file, and the host moves the
	 * finished export there. The document never passes through the page.
	 */
	async #saveAsOnDesktop(session: SessionDto, base: string, extension: string) {
		const desktop = this.#desktop;
		if (!desktop) return;
		this.error = null;
		// Busy from the dialog on, not from the export: a second press while the native dialog
		// is still up would otherwise open another one over it.
		this.exporting = true;
		try {
			const file = await desktop.pickSavePath(base + extension);
			if (!file) return;
			const result = await this.#client.hub.export(this.#id(), this.#request(session, false));
			const saved = await saveNatively(result, desktop, { file }, base, extension);
			toast.show(t('review.savedFiles', { n: saved.length }));
		} catch (e) {
			this.error = hubError(e);
		} finally {
			this.exporting = false;
		}
	}

	/**
	 * Save to server: the same export, written by the server into its configured folder
	 * instead of handed to the browser. The name is the one Save as… would suggest.
	 */
	async saveToServer() {
		const session = this.#session();
		if (!session) return;
		this.error = null;
		this.exporting = true;
		try {
			const result = await this.#client.hub.export(this.#id(), this.#request(session, true));
			const saved = result.Documents.map((d) => d.SavedTo).filter((p): p is string => !!p);
			const file = saved[0]?.split(/[/\\]/).pop() ?? '';
			// On the desktop the "server" is this machine, and its folder just a folder.
			const desktop = this.#desktop !== null;
			toast.show(
				saved.length === 1
					? t(desktop ? 'review.savedToFolderAs' : 'review.savedToServerAs', { file })
					: t(desktop ? 'review.savedFilesToFolder' : 'review.savedFilesToServer', {
							n: saved.length
						})
			);
		} catch (e) {
			this.error = hubError(e);
		} finally {
			this.exporting = false;
		}
	}

	/**
	 * Stops the feeder. The server keeps every page already scanned and moves the session to
	 * review.
	 */
	async stop() {
		this.error = null;
		this.stopping = true;
		try {
			await this.#client.hub.stopScan(this.#id());
		} catch (e) {
			this.error = hubError(e);
			this.stopping = false;
		}
	}

	scanMore() {
		return this.#attempt(() => this.#client.hub.scanMore(this.#id()));
	}

	/** Scans the page with this index again, in place. */
	rescan(index: number) {
		return this.#attempt(() => this.#client.hub.rescanPage(this.#id(), index));
	}

	/** Adds the pages of another scan to the end of this one. */
	async merge(sourceId: string) {
		this.mergeBusy = true;
		this.error = null;
		try {
			const before = this.#session()?.Pages.length ?? 0;
			const after = await this.#client.hub.mergeSession(this.#id(), sourceId);
			toast.show(t('review.pagesAdded', { n: after.Pages.length - before }));
		} catch (e) {
			this.error = hubError(e);
		} finally {
			this.mergeBusy = false;
		}
	}

	/**
	 * Crops the scanner's frame off these pages. Detection happens on the server, so there is
	 * no optimistic step: the pages update as the server's crops come back. Undo puts back the
	 * crops they had, which were snapshotted before asking.
	 */
	async autoCrop(targets: ReadonlySet<number>) {
		const session = this.#session();
		if (!session || this.autocropping) return;
		const before = session.Pages.filter((p) => targets.has(p.Index)).map((p) => ({
			index: p.Index,
			crop: p.Crop
		}));
		this.autocropping = true;
		this.error = null;
		try {
			const count = await this.#client.hub.autoCrop(
				this.#id(),
				before.map((p) => p.index)
			);
			if (count === 0) toast.show(t('review.noBorders'));
			else
				toast.show(t('review.croppedPages', { n: count }), () => {
					// A one-off argument, never kept or changed, so nothing needs to watch it.
					// eslint-disable-next-line svelte/prefer-svelte-reactivity
					for (const p of before) this.send(setCropEdit(p.crop, new Set([p.index])));
				});
		} catch (e) {
			this.error = hubError(e);
		} finally {
			this.autocropping = false;
		}
	}
}
