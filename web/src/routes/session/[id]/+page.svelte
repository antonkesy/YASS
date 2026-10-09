<script lang="ts">
	import { autoAnimate } from '$lib/autoAnimate';
	import { slide } from 'svelte/transition';
	import { ms } from '$lib/motion';
	import { untrack } from 'svelte';
	import { page as route } from '$app/state';
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { hubError } from '$lib/hub/errors';
	import { toast } from '$lib/toast';
	import { t } from '$lib/i18n/index.svelte';
	import { cameFrom } from '$lib/navigation';
	import { getDesktop, getHub, imageUrl, sortedSessions } from '$lib/hub/hub';
	import { SessionState } from '$lib/hub/generated/Yass.Core.Domain';
	import type { CropBox, PageAdjust, PageEdit } from '$lib/hub/generated/Yass.Core.Domain';
	import {
		activePageCount,
		documentName,
		colorLabel,
		exportFormatLabels,
		formatBytes,
		formatTime,
		pageSizeLabel,
		sessionStateLabel,
		sourceLabel
	} from '$lib/format';
	import {
		addCleanupEdit,
		applyOptimistic,
		commonCleanup,
		deleteEdit,
		removeCleanupEdit,
		reorderEdit,
		restoreEdit,
		rotateEdit,
		setCropEdit,
		setAdjustEdit,
		setFilterEdit,
		setSkewEdit
	} from '$lib/review/edits';
	import { clear, prune, toggle } from '$lib/review/selection';
	import { createPreloader, preloadOrder } from '$lib/review/preload';
	import { screenTransition } from '$lib/motion';
	import { modified, typing, useLayer } from '$lib/keys.svelte';
	import AdjustModal from '$lib/components/AdjustModal.svelte';
	import StraightenModal from '$lib/components/StraightenModal.svelte';
	import PageGrid from '$lib/components/PageGrid.svelte';
	import PageOverview from '$lib/components/PageOverview.svelte';
	import Toolbar from '$lib/components/Toolbar.svelte';
	import Preview from '$lib/components/Preview.svelte';
	import ExportBar from '$lib/components/ExportBar.svelte';
	import { Icon } from '$lib/icons';
	import NameField from '$lib/components/NameField.svelte';
	import CropModal from '$lib/components/CropModal.svelte';
	import MergeModal from '$lib/components/MergeModal.svelte';
	import StopScanButton from '$lib/components/StopScanButton.svelte';
	import {
		cancelled,
		exportBaseName,
		fileExtensions,
		savePicker,
		saveNatively,
		saveToBrowser,
		saveToDisk,
		type SaveTarget
	} from '$lib/review/download';
	import {
		DOCUMENT_META_FIELDS,
		documentMeta,
		documentMetadata,
		exportPrefs,
		metaFor,
		reviewPrefs,
		scannerPrefs,
		setDocumentMeta,
		type DocumentMeta
	} from '$lib/presets';
	import { scannerName } from '$lib/scanners';
	import type { MessageKey } from '$lib/i18n/index.svelte';
	import { ExportFormat } from '$lib/hub/generated/Yass.Core.Hub';
	import Segmented from '$lib/components/Segmented.svelte';

	const client = getHub();
	const desktop = getDesktop();
	const sessions = client.sessions;
	const connection = client.connection;
	const scannerStatus = client.scanner;

	/** The server's output folder, or "" when it has none; null until asked. */
	let serverFolder = $state<string | null>(null);
	$effect(() => {
		if ($connection === 'connected' && serverFolder === null) {
			client.hub
				.getServerFolder()
				.then((f) => (serverFolder = f))
				.catch(() => (serverFolder = ''));
		}
	});
	const exportProgress = client.exportProgress;

	const id = $derived(route.params.id ?? '');
	const session = $derived($sessions.get(id) ?? null);
	let missing = $state(false);
	let error = $state<string | null>(null);
	let selection = $state<Set<number>>(clear());
	let exporting = $state(false);
	/** Position in `pages` shown in the centre column. */
	let current = $state(0);
	/** Whether the filmstrip is expanded to the full-screen all-pages overview. */
	let overview = $state(false);

	/** The pages the Adjust dialog sets, and the one it shows and starts from. */
	/** The page the Straighten dialog is open on. */
	let straightening = $state<number | null>(null);
	let autocropping = $state(false);
	let adjusting = $state<{ pages: ReadonlySet<number>; from: number } | null>(null);
	let previewIndex = $state<number | null>(null);
	/** The big preview slides in from whichever side the new page came from. */
	let stage = $state<HTMLElement | null>(null);
	/** Hash of the page whose preview has finished loading onto the stage. */
	/** What the stage image last finished loading: which page, which render, and its turn. */
	let stageLoaded = $state<{ index: number; hash: string; rotation: number } | null>(null);
	// Deliberately the initial value: it tracks which page the stage is showing, and is
	// updated by the effect below rather than following `current` on its own.
	let shownAt = untrack(() => current);

	$effect(() => {
		const to = current;
		if (to === shownAt) return;
		const direction = to > shownAt ? 1 : -1;
		shownAt = to;
		stage?.animate(
			[
				{ opacity: 0, transform: `translateX(${direction * 36}px) scale(0.97)` },
				{ opacity: 1, transform: 'none' }
			],
			{ duration: 340, easing: 'cubic-bezier(.2,.8,.2,1)' }
		);
	});

	/** Position of the page the crop dialog is open on, or null. */
	let cropping = $state<number | null>(null);
	let merging = $state(false);
	let confirmDiscard = $state(false);
	let merged = $state(false);

	$effect(() => {
		if ($connection === 'connected' && !$sessions.has(id)) {
			client.refresh(id).catch(() => (missing = true));
		}
	});
	$effect(() => {
		const count = session?.Pages.length ?? 0;
		const pruned = prune(
			untrack(() => selection),
			count
		);
		if (pruned.size !== untrack(() => selection).size) selection = pruned;
		if (current >= count) current = Math.max(0, count - 1);
	});

	const scanning = $derived(session?.State === SessionState.Scanning);
	const editable = $derived(
		session?.State === SessionState.Review || session?.State === SessionState.Done
	);
	const activePages = $derived(session ? activePageCount(session) : 0);
	/** Why the sidebar's buttons are greyed while the scan is not in review, for their tooltips. */
	const busyReason = $derived(
		scanning
			? t('review.waitScan')
			: session?.State === SessionState.Exporting
				? t('review.waitSave')
				: t('review.notEditable')
	);
	const shown = $derived(session?.Pages[current] ?? null);

	/** What this scan saves with: Settings' export preferences, its own PDF properties on top. */
	const docPrefs = $derived(metaFor($exportPrefs, $documentMeta, id));
	let propertiesOpen = $state(false);
	const META_LABELS: Record<keyof DocumentMeta, MessageKey> = {
		author: 'settings.author',
		subject: 'settings.subject',
		keywords: 'settings.keywords'
	};
	/** The properties as one line, for the closed disclosure. */
	const metaSummary = $derived(
		DOCUMENT_META_FIELDS.map((f) => docPrefs[f].trim())
			.filter(Boolean)
			.join(' · ')
	);
	const formatOptions = $derived(
		[ExportFormat.Pdf, ExportFormat.Jpg, ExportFormat.Png].map((f) => ({
			value: String(f),
			label: exportFormatLabels[f]
		}))
	);
	/** The scanner the scan ran on, by the name this browser gives it. */
	const scannerLabel = $derived(
		session?.OptionsUsed.Device
			? scannerName($scannerPrefs, session.OptionsUsed.Device)
			: ($scannerStatus?.DeviceName ?? '')
	);
	/** The settings the scan ran with, each on its own tile; one the scan did not record is left out. */
	const scanSettings = $derived.by((): [MessageKey, string][] => {
		const o = session?.OptionsUsed;
		if (!o) return [];
		const all: [MessageKey, string][] = [
			['settings.source', sourceLabel(o.Source)],
			['settings.resolution', o.Dpi ? t('home.dpi', { n: o.Dpi }) : ''],
			['settings.colourMode', colorLabel(o.Color)],
			['settings.paperSize', pageSizeLabel(o.PageSize)]
		];
		return all.filter(([, value]) => value);
	});
	/** The page on the stage has an image up — maybe of an older render of it, during an edit. */
	const stageReady = $derived(!!shown && stageLoaded?.index === shown.Index);
	/**
	 * A rotation not yet in the image on screen. The page keeps showing its last render, turned
	 * by CSS, until the new one has loaded: swapping straight to an empty frame with a spinner
	 * in front made every rotate blink.
	 */
	const stageTurn = $derived(stageReady && shown ? shown.Rotation - stageLoaded!.rotation : 0);
	// A view transition already slides the screen in; its own entrance would be a second slide.
	const entrance = screenTransition.active ? '' : 'animate-slide-from-right';

	/**
	 * Warms the previews around whichever page is in view — the full-screen preview's if it is
	 * open, else the stage's — so stepping to the next one does not wait for the server to render
	 * it. Held off during a scan, when the render slots belong to the thumbnails coming in.
	 */
	const preloader = createPreloader();
	$effect(() => {
		const pages = session?.Pages;
		if (!pages || scanning) return preloader.stop();
		const from = previewIndex ?? current;
		preloader.want(
			preloadOrder(pages.length, from).map((i) =>
				imageUrl(id, pages[i].Index, pages[i].Hash, 'preview')
			)
		);
	});
	$effect(() => () => preloader.stop());

	/**
	 * What the toolbar acts on. Nothing ticked means the page on the stage rather than every
	 * page: the buttons sit under that page, Crop and Rescan have always read it that way,
	 * and a rotation of the whole scan is worth asking for — the toolbar's All does that.
	 */
	const targets = $derived(selection.size > 0 ? selection : new Set(shown ? [shown.Index] : []));
	/** The clean-ups every targeted page already has, which the filter menu shows ticked. */
	const targetCleanup = $derived(
		commonCleanup(session?.Pages.filter((p) => targets.has(p.Index)) ?? [])
	);

	const anyDeleted = $derived(
		session?.Pages.filter((p) => targets.has(p.Index)).some((p) => p.Deleted) ?? false
	);
	/** The other half of it: what Delete would still have to work on. */
	const anyActive = $derived(
		session?.Pages.filter((p) => targets.has(p.Index)).some((p) => !p.Deleted) ?? false
	);
	/**
	 * What the Scan section says while it is shut. Worded exactly as the line it replaces, which
	 * sat under the download button: it is a fact about the paper, not about the saving.
	 */
	const pageSummary = $derived(
		session ? t('review.pageSummary', { active: activePages, n: session.Pages.length }) : ''
	);

	/** Other scans that still have pages, newest first, for the merge dialog. */
	const mergeable = $derived(
		sortedSessions($sessions).filter((s) => s.Id !== id && s.Pages.some((p) => !p.Deleted))
	);

	async function send(edit: PageEdit) {
		if (!session) return;
		error = null;
		client.updateLocal(id, (s) => applyOptimistic(s, edit));
		try {
			await client.hub.applyEdits(id, [edit]);
		} catch (e) {
			error = hubError(e);
			await client.refresh(id);
		}
	}

	/** Discard throws the scan away entirely: it leaves no row behind in past scans. */
	async function discard() {
		try {
			await client.hub.deleteSession(id);
			cameFrom.leaving(id);
			await goto(resolve('/'));
			toast.show(t('review.scanDiscarded'));
		} catch (e) {
			error = hubError(e);
		}
	}

	/**
	 * The name this browser last asked for, held until the session comes back carrying it.
	 * Hub calls run one at a time per connection, so a rename can sit behind a page read —
	 * and Save as… must not name the file from a session that has not caught up yet.
	 */
	let renamedTo = $state<string | null>(null);
	const storedName = $derived(renamedTo ?? session?.Name ?? null);

	// Held until the session comes back carrying it: the call returning is not the same as the
	// session this page reads from having caught up, and dropping it in between would leave
	// Save as… naming the file from a session that still thinks it is unnamed.
	$effect(() => {
		if (renamedTo !== null && (session?.Name ?? '') === renamedTo) renamedTo = null;
	});

	async function rename(name: string) {
		renamedTo = name.trim();
		try {
			await client.hub.renameSession(id, name);
		} catch (e) {
			renamedTo = null;
			error = hubError(e);
		}
	}

	/** The name field wins; the template is what an unnamed scan falls back to. */
	const exportName = $derived(storedName?.trim() || $exportPrefs.nameTemplate.trim() || undefined);

	/**
	 * Save as…: asks where the file goes first, while the click is still a user gesture —
	 * the export itself takes long enough that a browser would refuse to open a dialog
	 * afterwards — then streams the finished document straight there. Browsers without
	 * the File System Access API, which needs a secure context, get a plain download.
	 */
	async function saveAs() {
		const picker = savePicker();
		let target: SaveTarget | null = null;
		// The name this browser asked for, not the one the session has caught up to — a rename
		// can still be in the air behind a page read when Save as… is pressed.
		const base = session
			? exportBaseName({ ...session, Name: storedName ?? undefined }, $exportPrefs.nameTemplate)
			: 'scan';
		const extension = fileExtensions[$exportPrefs.format];

		if (desktop) {
			await saveAsOnDesktop(base, extension);
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

		error = null;
		exporting = true;
		try {
			const result = await client.hub.export(id, {
				Format: $exportPrefs.format,
				Name: exportName,
				Metadata: documentMetadata(docPrefs),
				ToServer: false
			});
			const count = target
				? await saveToDisk(result, target, base, extension)
				: saveToBrowser(result);
			toast.show(t(target ? 'review.savedFiles' : 'review.downloadedFiles', { n: count }));
		} catch (e) {
			error = hubError(e);
		} finally {
			exporting = false;
		}
	}

	/**
	 * The desktop app's Save as…: the native dialog picks the file, and the host moves the
	 * finished export there. The document never passes through the page.
	 */
	async function saveAsOnDesktop(base: string, extension: string) {
		if (!desktop) return;
		error = null;
		try {
			const file = await desktop.pickSavePath(base + extension);
			if (!file) return;
			exporting = true;
			const result = await client.hub.export(id, {
				Format: $exportPrefs.format,
				Name: exportName,
				Metadata: documentMetadata(docPrefs),
				ToServer: false
			});
			const saved = await saveNatively(result, desktop, { file }, base, extension);
			toast.show(t('review.savedFiles', { n: saved.length }));
		} catch (e) {
			error = hubError(e);
		} finally {
			exporting = false;
		}
	}

	/**
	 * Stops the feeder. The server keeps every page already scanned and moves the session to
	 * review; the SessionChanged that follows is what takes the stop button away.
	 */
	let stopping = $state(false);
	async function stopScan() {
		error = null;
		stopping = true;
		try {
			await client.hub.stopScan(id);
		} catch (e) {
			error = hubError(e);
			stopping = false;
		}
	}
	$effect(() => {
		if (stopping && !scanning) {
			stopping = false;
			toast.show(t('review.scanStopped'));
		}
	});

	/**
	 * Save to server: the same export, written by the server into its configured folder
	 * instead of handed to the browser. The name is the one Save as… would suggest.
	 */
	async function saveToServer() {
		if (!session) return;
		error = null;
		exporting = true;
		try {
			const result = await client.hub.export(id, {
				Format: $exportPrefs.format,
				Name: exportName,
				Metadata: documentMetadata(docPrefs),
				ToServer: true
			});
			const saved = result.Documents.map((d) => d.SavedTo).filter((p): p is string => !!p);
			const file = saved[0]?.split(/[/\\]/).pop() ?? '';
			// On the desktop the "server" is this machine, and its folder just a folder.
			toast.show(
				saved.length === 1
					? t(desktop ? 'review.savedToFolderAs' : 'review.savedToServerAs', { file })
					: t(desktop ? 'review.savedFilesToFolder' : 'review.savedFilesToServer', {
							n: saved.length
						})
			);
		} catch (e) {
			error = hubError(e);
		} finally {
			exporting = false;
		}
	}

	/** Tapping a page in the strip only moves the stage; the checkbox is what selects. */
	function show(index: number) {
		const at = session?.Pages.findIndex((p) => p.Index === index) ?? -1;
		if (at >= 0) current = at;
	}

	function select(index: number) {
		selection = toggle(selection, index);
	}

	/**
	 * A reorder renumbers every page, so the ticks would end up on pages nobody picked.
	 */
	function reorder(order: number[]) {
		selection = clear();
		send(reorderEdit(order));
	}

	function selectAll() {
		selection = new Set((session?.Pages ?? []).map((p) => p.Index));
	}

	/**
	 * Ticking pages is the all-pages view's business, so the ticks leave with it. The review
	 * screen shows one page and its toolbar edits that page; a selection surviving the close
	 * would leave those buttons acting on pages that are no longer on screen.
	 */
	function closeOverview() {
		overview = false;
		selection = clear();
	}

	/** Crop and rescan act on the one selected page, or the one on screen. */
	function deletePages() {
		// A snapshot, because undo has to put back the pages this click took and not
		// whatever happens to be on the stage by the time the toast is pressed.
		const deleting = new Set(targets);
		send(deleteEdit(deleting));
		toast.show(t('review.pagesDeleted', { n: deleting.size }), () => send(restoreEdit(deleting)));
	}

	/** Escape with nothing open goes home, as the header's back link does. */
	const screen = useLayer(() => {
		cameFrom.leaving(id);
		void goto(resolve('/'));
	});

	/**
	 * The review screen's keys, only while nothing is open over it and nobody is typing:
	 * pages with the arrows, the toolbar's edits on single letters, Ctrl/⌘+S for Save as….
	 */
	function keydown(e: KeyboardEvent) {
		if (!session || !screen()?.top || typing(e)) return;
		if ((e.ctrlKey || e.metaKey) && !e.altKey && e.key.toLowerCase() === 's') {
			e.preventDefault();
			void saveAs();
			return;
		}
		if (modified(e)) return;
		const last = session.Pages.length - 1;
		switch (e.key) {
			case 'ArrowLeft':
				current = Math.max(0, current - 1);
				break;
			case 'ArrowRight':
				current = Math.min(last, current + 1);
				break;
			case 'Enter':
				// Enter on a focused button or link presses that, as it always has.
				if (last < 0 || (e.target as HTMLElement).closest('button, a, [role]')) return;
				previewIndex = targetPage();
				break;
			case 'a':
			case 'A':
				if (last < 0) return;
				overview = true;
				break;
			case 'r':
			case 'R':
				if (!editable) return;
				send(rotateEdit(e.shiftKey ? -90 : 90, targets));
				break;
			case 'Delete':
			case 'Backspace':
				if (!editable || !anyActive) return;
				deletePages();
				break;
			default:
				return;
		}
		e.preventDefault();
	}

	function targetPage() {
		if (selection.size === 1) {
			const only = [...selection][0];
			const at = session?.Pages.findIndex((p) => p.Index === only) ?? -1;
			if (at >= 0) return at;
		}
		return current;
	}

	async function scanMore() {
		error = null;
		try {
			await client.hub.scanMore(id);
		} catch (e) {
			error = hubError(e);
		}
	}

	async function rescan() {
		const page = session?.Pages[targetPage()];
		if (!page) return;
		error = null;
		try {
			await client.hub.rescanPage(id, page.Index);
		} catch (e) {
			error = hubError(e);
		}
	}

	/**
	 * What the field shows, and the same thing the list of past scans shows for this scan: a
	 * scan nobody has named reads as the template from settings, which is what it will be
	 * saved as.
	 */
	const displayName = $derived(
		session
			? documentName({ ...session, Name: storedName ?? undefined }, $exportPrefs.nameTemplate)
			: null
	);

	async function merge(sourceId: string) {
		merging = false;
		merged = true;
		error = null;
		try {
			const before = session?.Pages.length ?? 0;
			const after = await client.hub.mergeSession(id, sourceId);
			toast.show(t('review.pagesAdded', { n: after.Pages.length - before }));
		} catch (e) {
			error = hubError(e);
		} finally {
			merged = false;
		}
	}

	function applyAdjust(adjust: PageAdjust | undefined) {
		if (!adjusting) return;
		const pages = adjusting.pages;
		adjusting = null;
		send(setAdjustEdit(adjust, pages));
	}

	/**
	 * Crops the scanner's frame off the pages in frame. Detection happens on the server, so
	 * there is no optimistic step: the pages update as the server's crops come back. Undo puts
	 * back the crops they had, which were snapshotted before asking.
	 */
	async function autoCrop() {
		if (!session || autocropping) return;
		const before = session.Pages.filter((p) => targets.has(p.Index)).map((p) => ({
			index: p.Index,
			crop: p.Crop
		}));
		autocropping = true;
		error = null;
		try {
			const count = await client.hub.autoCrop(
				id,
				before.map((p) => p.index)
			);
			if (count === 0) toast.show(t('review.noBorders'));
			else
				toast.show(t('review.croppedPages', { n: count }), () => {
					for (const p of before) send(setCropEdit(p.crop, new Set([p.index])));
				});
		} catch (e) {
			error = hubError(e);
		} finally {
			autocropping = false;
		}
	}

	function applySkew(degrees: number) {
		const page = straightening !== null ? session?.Pages[straightening] : null;
		straightening = null;
		if (page) send(setSkewEdit(degrees, new Set([page.Index])));
	}

	function applyCrop(crop: CropBox | undefined) {
		const page = cropping !== null ? session?.Pages[cropping] : null;
		cropping = null;
		if (page) send(setCropEdit(crop, new Set([page.Index])));
	}

	const ghost =
		'flex h-[42px] items-center gap-2.5 rounded-[12px] px-2.5 text-left text-sm font-semibold transition-colors hover:bg-accent-hover50 disabled:pointer-events-none disabled:opacity-40';

	function step(delta: number) {
		const count = session?.Pages.length ?? 0;
		current = Math.min(count - 1, Math.max(0, current + delta));
	}

	$effect(() => {
		if (previewIndex !== null && session && previewIndex >= session.Pages.length)
			previewIndex = null;
	});
</script>

<svelte:window onkeydown={keydown} />

<svelte:head>
	<title>{session ? formatTime(session.CreatedAt) : t('review.sessionTitle')} · YASS</title>
</svelte:head>

{#snippet heading(title: string, value = '', testid = '')}
	<div class="flex items-baseline gap-2">
		<span class="text-[13px] font-semibold text-ink-muted">{title}</span>
		{#if value}
			<span
				class="ml-auto min-w-0 truncate text-[13px] font-semibold text-ink-muted"
				data-testid={testid || undefined}>{value}</span
			>
		{/if}
	</div>
{/snippet}

{#snippet tile(label: string, value: string, wide = false)}
	<!-- Label over value, two to a row: the facts of a scan read at a glance in a narrow card. -->
	<div
		class="flex min-w-0 flex-col gap-0.5 rounded-[12px] bg-app px-2.5 py-2 {wide
			? 'col-span-2'
			: ''}"
	>
		<span class="truncate text-[11px] text-ink-muted">{label}</span>
		<span class="truncate text-[13px] font-bold tracking-[-0.01em] tabular-nums" title={value}
			>{value}</span
		>
	</div>
{/snippet}

{#snippet pageTools(scoped: boolean)}
	{#if session}
		<Toolbar
			{scoped}
			selectedCount={selection.size}
			total={session.Pages.length}
			{anyDeleted}
			{anyActive}
			disabled={!editable}
			onrotate={(deg) => send(rotateEdit(deg, targets))}
			ondelete={deletePages}
			onrestore={() => send(restoreEdit(targets))}
			onfilter={(filter) => send(setFilterEdit(filter, targets))}
			cleanup={targetCleanup}
			oncleanup={(cleanup, on) =>
				send(on ? addCleanupEdit(cleanup, targets) : removeCleanupEdit(cleanup, targets))}
			onexpand={() => (previewIndex = targetPage())}
			oncrop={() => (cropping = targetPage())}
			onstraighten={() => (straightening = targetPage())}
			onautocrop={autoCrop}
			{autocropping}
			onadjust={() => (adjusting = { pages: new Set(targets), from: targetPage() })}
			onrescan={rescan}
			{scanning}
			onclear={() => (selection = clear())}
			onselectall={selectAll}
		/>
	{/if}
{/snippet}

<!--
	Side by side, the three panes get exactly the height left over after the header and
	this container's own padding -- so the review screen fits without scrolling
	and each pane scrolls its own overflow instead. Stacked (below lg) they keep their
	natural height and the page scrolls, which is the right behaviour on a phone.
-->
<div
	class="mx-auto flex max-w-[2200px] {entrance} flex-col px-4 pt-2 pb-7 sm:px-7 lg:h-[calc(100dvh-var(--app-header-h))] lg:min-h-0"
>
	{#if missing}
		<p class="rounded-[14px] bg-danger-bg px-4 py-3 font-semibold text-danger" role="alert">
			{t('review.sessionGone')}
		</p>
	{:else if !session}
		<p class="text-ink-muted">{t('review.loading')}</p>
	{:else}
		{#if session.Error}
			<p
				class="mb-3 rounded-[14px] bg-warning-bg px-4 py-2 font-semibold text-warning-ink"
				role="alert"
			>
				{session.Error}
			</p>
		{/if}
		{#if error}
			<p class="mb-3 rounded-[14px] bg-danger-bg px-4 py-2 font-semibold text-danger" role="alert">
				{error}
			</p>
		{/if}

		<div class="flex min-h-0 flex-wrap items-start gap-5 lg:flex-1 lg:flex-nowrap lg:items-stretch">
			<!-- Filmstrip: down the left on desktop, above the page on a phone. -->
			<nav
				class="order-2 w-full flex-none lg:order-1 lg:min-h-0 lg:w-auto"
				aria-label={t('review.pages')}
			>
				<PageGrid
					sessionId={id}
					pages={session.Pages}
					{selection}
					current={session.Pages[current]?.Index ?? -1}
					{scanning}
					onshow={show}
					ontoggle={select}
					onopen={(i) => (previewIndex = i)}
					onscanmore={editable ? scanMore : undefined}
					reorderable={editable}
					onreorder={reorder}
					onexpand={() => (overview = true)}
				/>
			</nav>

			<main
				class="order-1 flex w-full flex-1 basis-full flex-col items-center gap-[18px] lg:order-2 lg:min-h-0 lg:basis-[380px]"
			>
				<!--
					The row holds the state chip once a save has started: the save is running, it
					failed, it is saved — things the screen does not otherwise say. Scanning gets
					no chip: the header's scanner pill already says so, with the page count, and in
					review the row goes too rather than leaving a gap above the page.
				-->
				{#if scanning}
					<!--
						On a phone the save panel, and the Stop in it, is below the page and the
						strip; this one is where the thumb already is. Side by side the panel's own
						is in view, so this one goes.
					-->
					<div class="w-full max-w-[420px] lg:hidden">
						<StopScanButton onstop={stopScan} {stopping} testid="stop-scan-top" />
					</div>
				{/if}
				{#if session.State !== SessionState.Review && !scanning}
					<div class="flex w-full flex-wrap items-center justify-center gap-2">
						<span
							class="rounded-full bg-surface px-3 py-1.5 text-[13px] font-bold shadow-chip"
							data-testid="session-state"
						>
							{sessionStateLabel(session.State)}
						</span>
					</div>
				{/if}

				{#if session.Pages.length > 0}
					<!-- Right above the page it counts, and as few characters as say it. -->
					<p
						class="-mb-2 text-[13px] font-semibold text-ink-muted tabular-nums"
						data-testid="page-position"
					>
						{current + 1} / {session.Pages.length}
					</p>
				{/if}

				{#if shown}
					<!--
						At either end of the scan the arrow that has nowhere to go is not shown, but its
						place is kept (disabled:invisible), so the page stays centred between the two.
					-->
					<div class="flex w-full items-center justify-center gap-2 lg:min-h-0 lg:flex-1">
						<button
							type="button"
							class="flex h-11 w-11 flex-none items-center justify-center rounded-full bg-surface shadow-nav transition-[background-color,transform] duration-150 hover:scale-105 hover:bg-accent-wash disabled:invisible"
							onclick={() => step(-1)}
							disabled={current === 0}
							aria-label={t('review.previousPage')}
							data-testid="stage-prev"
						>
							<Icon name="chevronLeft" />
						</button>

						<!--
							The box is reserved at page proportions so the column does not reflow when
							the image arrives; a rotated page letterboxes inside it.
						-->
						<!-- Beside the stage the page is as tall as the pane allows, and as wide
						     as that height makes it; stacked it is as wide as the phone allows. -->
						<div
							bind:this={stage}
							class="relative w-full max-w-[max(280px,calc((100dvh-230px)/1.414))] flex-1 lg:h-full lg:w-auto lg:max-w-none lg:flex-none"
							style="aspect-ratio: 1 / 1.414"
							aria-busy={stageLoaded?.hash !== shown.Hash}
						>
							<!-- A fresh element per page: swapping only the src keeps the previous page on
							     screen until the new one has decoded, which reads as the arrows not working. -->
							<!--
								A fresh element per page, so the arrows never seem to do nothing; within a
								page the same element, so an edit keeps the last render up until the new
								one replaces it.
							-->
							{#key shown.Index}
								<img
									src={imageUrl(id, shown.Index, shown.Hash, 'preview')}
									alt={t('review.pageN', { n: shown.Index + 1 })}
									class="h-full w-full rounded-[6px] object-contain drop-shadow-[0_18px_36px_rgb(0_0_0_/_0.14)]"
									style:transform={stageTurn
										? `rotate(${stageTurn}deg) scale(${Math.abs(stageTurn) % 180 ? 0.707 : 1})`
										: undefined}
									onload={() =>
										(stageLoaded = {
											index: shown.Index,
											hash: shown.Hash,
											rotation: shown.Rotation
										})}
								/>
							{/key}
							{#if !stageReady}
								<!-- The preview is rendered on demand, so the empty box sits there for a
								     moment; the same spinner the thumbnails use says it is on the way. -->
								<span
									class="pointer-events-none absolute inset-0 flex items-center justify-center"
									role="status"
									aria-label={t('review.loadingPage', { n: shown.Index + 1 })}
									data-testid="stage-loading"
								>
									<span
										class="h-9 w-9 animate-spin rounded-full border-[3px] border-accent-100 border-t-accent"
										aria-hidden="true"
									></span>
								</span>
							{/if}
						</div>

						<button
							type="button"
							class="flex h-11 w-11 flex-none items-center justify-center rounded-full bg-surface shadow-nav transition-[background-color,transform] duration-150 hover:scale-105 hover:bg-accent-wash disabled:invisible"
							onclick={() => step(1)}
							disabled={current >= session.Pages.length - 1}
							aria-label={t('review.nextPage')}
							data-testid="stage-next"
						>
							<Icon name="chevronRight" />
						</button>
					</div>
				{:else}
					<div
						class="flex w-full max-w-[280px] flex-col items-center justify-center gap-3 rounded-[10px] border-[1.5px] border-dashed border-accent-dash text-sm text-ink-muted"
						style="aspect-ratio: 1 / 1.414"
						role={scanning ? 'status' : undefined}
						data-testid="stage-empty"
					>
						{#if scanning}
							<!-- The same spinner a thumbnail shows while its image is on the way. -->
							<span
								class="h-9 w-9 animate-spin rounded-full border-[3px] border-accent-100 border-t-accent"
								aria-hidden="true"
								data-testid="stage-spinner"
							></span>
							{t('review.waitingFirstPage')}
						{:else}
							{t('review.noPages')}
						{/if}
					</div>
				{/if}

				{@render pageTools(false)}
			</main>

			<!--
				Width kept close to the filmstrip's: the stage sits between the two, so the page
				on screen is only centred while the columns either side of it match. Neither can
				be the other's exact width — thumbnails do not want 280px and this card does not
				want 180 — so they meet in the middle and the page lands near enough to centre.
			-->
			<aside
				class="order-3 flex w-full min-w-0 basis-full flex-col gap-3.5 rounded-[24px] bg-surface p-[22px] shadow-panel lg:min-h-0 lg:w-auto lg:basis-(--review-aside-w) lg:overflow-y-auto"
				{@attach autoAnimate}
			>
				{@render heading(t('review.headingName'))}

				<NameField
					value={displayName}
					placeholder={exportBaseName(session, $exportPrefs.nameTemplate)}
					extension={fileExtensions[$exportPrefs.format]}
					onrename={rename}
				/>

				{@render heading(t('review.headingSave'))}

				<ExportBar
					{session}
					progress={$exportProgress[id] ?? null}
					busy={exporting}
					picker={desktop !== null || savePicker() !== null}
					ondownload={saveAs}
					serverFolder={$exportPrefs.serverSave && serverFolder ? serverFolder : null}
					onserver={saveToServer}
					onstop={stopScan}
					{stopping}
				/>

				<!-- What the file will be, together under the button that saves it. -->
				<div class="flex flex-col rounded-[16px] bg-app p-3" data-testid="file-options">
					<!-- Settings → Export holds the same choice; switching here switches it there. -->
					<Segmented
						name="review-format"
						label={t('settings.fileFormat')}
						hint={$exportPrefs.format === ExportFormat.Pdf
							? t('settings.perDocument')
							: t('settings.perPage')}
						options={formatOptions}
						value={String($exportPrefs.format)}
						onchange={(v) =>
							exportPrefs.set({ ...$exportPrefs, format: Number(v) as ExportFormat })}
					/>

					{#if $exportPrefs.format === ExportFormat.Pdf}
						<!--
							This scan's own PDF properties, starting from the ones Settings gives every scan.
							Shut by default: a line saying what is set is all most saves need to see.
						-->
						<!-- The gap above is its own padding, so it opens and shuts with it. -->
						<div
							class="flex flex-col gap-2 pt-3"
							transition:slide={{ duration: ms(220) }}
							data-testid="pdf-properties"
						>
							<button
								type="button"
								class="flex items-center gap-2 rounded-[10px] text-left"
								aria-expanded={propertiesOpen}
								onclick={() => (propertiesOpen = !propertiesOpen)}
								data-testid="pdf-properties-toggle"
							>
								<span class="text-[13px] font-semibold text-ink-muted"
									>{t('review.pdfProperties')}</span
								>
								<span
									class="ml-auto min-w-0 truncate text-[12px] text-ink-muted"
									title={metaSummary}>{metaSummary || t('review.pdfNone')}</span
								>
								<span
									class="flex-none text-ink-muted transition-transform {propertiesOpen
										? 'rotate-90'
										: ''}"
									aria-hidden="true"><Icon name="chevronRight" /></span
								>
							</button>
							{#if propertiesOpen}
								<div
									class="flex flex-col gap-2"
									transition:slide={{ duration: ms(220) }}
									data-testid="pdf-fields"
								>
									{#each DOCUMENT_META_FIELDS as field (field)}
										<label class="flex flex-col gap-1">
											<span class="text-xs text-ink-muted">{t(META_LABELS[field])}</span>
											<input
												class="field h-9 px-2.5 text-[13px]"
												type="text"
												autocomplete="off"
												value={docPrefs[field]}
												oninput={(e) =>
													documentMeta.set(
														setDocumentMeta(
															$documentMeta,
															id,
															$exportPrefs,
															field,
															e.currentTarget.value
														)
													)}
												data-testid="pdf-{field}"
											/>
										</label>
									{/each}
								</div>
							{/if}
						</div>
					{/if}
				</div>

				<!-- The facts about the scan, with the page count up on the heading beside them. -->
				<div class="flex flex-col gap-1.5" data-testid="scan-section">
					{@render heading(t('review.headingScan'), pageSummary, 'page-count')}
					<div class="mt-1 grid grid-cols-2 gap-1.5">
						{@render tile(t('review.scanned'), formatTime(session.CreatedAt))}
						<!-- The scanned pages on disk, not an estimate of the PDF the mockup guesses at. -->
						{@render tile(t('review.size'), formatBytes(session.SizeBytes))}
						{#if scannerLabel}
							{@render tile(t('review.scanner'), scannerLabel, true)}
						{/if}
						{#each scanSettings as [label, value] (label)}
							{@render tile(t(label), value)}
						{/each}
					</div>
				</div>

				<div class="flex flex-col gap-0.5" data-testid="more-section">
					{@render heading(t('review.headingMore'))}
					<!-- A greyed button says why on hover: rule of thumb, no dead control without a reason. -->
					<button
						type="button"
						class={ghost}
						onclick={scanMore}
						disabled={!editable}
						title={editable ? undefined : busyReason}
						data-testid="scan-more-button"
					>
						<Icon name="plus" />
						{t('review.scanMorePages')}
					</button>
					<button
						type="button"
						class={ghost}
						onclick={() => (merging = true)}
						disabled={!editable || mergeable.length === 0}
						title={!editable
							? busyReason
							: mergeable.length === 0
								? t('review.noMergeable')
								: undefined}
						data-testid="merge-button"
					>
						<Icon name="layers" />
						{t('review.addFromPastScan')}
					</button>
				</div>

				<!--
					Alone at the foot, because it throws the scan away and has no business sitting
					where a hand goes looking for Scan more. mt-auto puts it on the bottom edge
					while the panel has room to spare, and is simply inert once it has not.
				-->
				<div class="mt-auto border-t border-line pt-4 max-lg:mt-4">
					<button
						type="button"
						class="{ghost} w-full text-danger hover:bg-danger-bg"
						onclick={() => (confirmDiscard ? discard() : (confirmDiscard = true))}
						data-testid="discard-button"
					>
						<Icon name="trash" />
						{confirmDiscard ? t('review.reallyDiscard') : t('review.discardScan')}
					</button>
				</div>
			</aside>
		</div>
	{/if}
</div>

{#if merging}
	<MergeModal
		sessions={mergeable}
		template={$exportPrefs.nameTemplate}
		busy={merged}
		onpick={merge}
		oncancel={() => (merging = false)}
	/>
{/if}

{#if adjusting && session?.Pages[adjusting.from]}
	{@const from = session.Pages[adjusting.from]}
	<AdjustModal
		src={imageUrl(id, from.Index, from.Hash, 'preview')}
		alt={t('review.pageN', { n: from.Index + 1 })}
		adjust={from.Adjust ?? null}
		count={adjusting.pages.size}
		onapply={applyAdjust}
		oncancel={() => (adjusting = null)}
	/>
{/if}

{#if straightening !== null && session?.Pages[straightening]}
	{@const page = session.Pages[straightening]}
	<StraightenModal
		src={imageUrl(id, page.Index, page.Hash, 'preview')}
		alt={t('review.pageN', { n: page.Index + 1 })}
		skew={page.Skew}
		onapply={applySkew}
		oncancel={() => (straightening = null)}
	/>
{/if}

{#if cropping !== null && session?.Pages[cropping]}
	<CropModal
		src={imageUrl(id, session.Pages[cropping].Index, session.Pages[cropping].Hash, 'frame')}
		rotation={session.Pages[cropping].Rotation}
		alt={t('review.pageN', { n: session.Pages[cropping].Index + 1 })}
		crop={session.Pages[cropping].Crop ?? null}
		onapply={applyCrop}
		oncancel={() => (cropping = null)}
	/>
{/if}

<!-- Outside the container above: an animated ancestor would trap these overlays under the header. -->
{#if overview && session}
	<PageOverview
		sessionId={id}
		pages={session.Pages}
		{selection}
		current={session.Pages[current]?.Index ?? -1}
		{scanning}
		reorderable={editable}
		tile={$reviewPrefs.overviewTile}
		onzoom={(overviewTile) => reviewPrefs.set({ ...$reviewPrefs, overviewTile })}
		onshow={show}
		ontoggle={select}
		onopen={(i) => (previewIndex = i)}
		onreorder={reorder}
		onselectall={selectAll}
		onclose={closeOverview}
	>
		{@render pageTools(true)}
	</PageOverview>
{/if}

{#if previewIndex !== null && session}
	<Preview
		sessionId={id}
		pages={session.Pages}
		index={previewIndex}
		onnavigate={(i) => (previewIndex = i)}
		onclose={() => (previewIndex = null)}
		onrotate={(i, deg) => send(rotateEdit(deg, new Set([i])))}
		editable={editable && !scanning}
		paused={cropping !== null || straightening !== null || adjusting !== null}
		onadjust={(i) => (adjusting = { pages: new Set([i]), from: i })}
		oncrop={(i) => (cropping = i)}
		onstraighten={(i) => (straightening = i)}
		onfilter={(i, filter) => send(setFilterEdit(filter, new Set([i])))}
		oncleanup={(i, cleanup, on) =>
			send(on ? addCleanupEdit(cleanup, new Set([i])) : removeCleanupEdit(cleanup, new Set([i])))}
	/>
{/if}
