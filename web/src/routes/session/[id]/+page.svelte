<script lang="ts">
	import { untrack } from 'svelte';
	import { page as route } from '$app/state';
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { hubError } from '$lib/hub/errors';
	import { toast } from '$lib/toast';
	import { cameFrom } from '$lib/navigation';
	import { getHub, imageUrl, sortedSessions } from '$lib/hub/hub';
	import { PageFilter, SessionState } from '$lib/hub/generated/Yass.Core.Domain';
	import type { CropBox, PageEdit } from '$lib/hub/generated/Yass.Core.Domain';
	import {
		activePageCount,
		documentName,
		formatBytes,
		formatTime,
		sessionStateLabel
	} from '$lib/format';
	import {
		applyOptimistic,
		deleteEdit,
		reorderEdit,
		restoreEdit,
		rotateEdit,
		setCropEdit,
		setFilterEdit,
		pageFilterLabels
	} from '$lib/review/edits';
	import { clear, prune, toggle } from '$lib/review/selection';
	import PageGrid from '$lib/components/PageGrid.svelte';
	import PageOverview from '$lib/components/PageOverview.svelte';
	import Toolbar from '$lib/components/Toolbar.svelte';
	import Preview from '$lib/components/Preview.svelte';
	import ExportBar from '$lib/components/ExportBar.svelte';
	import { Icon } from '$lib/icons';
	import NameField from '$lib/components/NameField.svelte';
	import CropModal from '$lib/components/CropModal.svelte';
	import MergeModal from '$lib/components/MergeModal.svelte';
	import {
		cancelled,
		exportBaseName,
		fileExtensions,
		savePicker,
		saveToBrowser,
		saveToDisk,
		savedLabel,
		type SaveTarget
	} from '$lib/review/download';
	import { exportPrefs, reviewPrefs } from '$lib/presets';
	import { ExportMode } from '$lib/hub/generated/Yass.Core.Hub';

	const client = getHub();
	const sessions = client.sessions;
	const connection = client.connection;
	const exportProgress = client.exportProgress;

	const id = $derived(route.params.id ?? '');
	const session = $derived($sessions.get(id) ?? null);
	let missing = $state(false);
	let error = $state<string | null>(null);
	let selection = $state<Set<number>>(clear());
	let exporting = $state(false);
	let savedPath = $state<string | null>(null);
	let allowDownload = $state(false);
	/** Folders the server will accept, from output.allowed_roots. */
	let exportRoots = $state<string[]>([]);
	/** Position in `pages` shown in the centre column. */
	let current = $state(0);
	/** Whether the filmstrip is expanded to the full-screen all-pages overview. */
	let overview = $state(false);

	$effect(() => {
		if ($connection === 'connected') {
			client.hub
				.getEffectiveOptions()
				.then((o) => {
					allowDownload = o.AllowDownload ?? false;
					exportRoots = o.ExportRoots ?? [];
				})
				.catch(() => undefined);
		}
	});
	let previewIndex = $state<number | null>(null);
	/** The big preview slides in from whichever side the new page came from. */
	let stage = $state<HTMLElement | null>(null);
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
			? 'Wait for the scan to finish'
			: session?.State === SessionState.Exporting
				? 'Wait for the save to finish'
				: 'This scan can no longer be edited'
	);
	const shown = $derived(session?.Pages[current] ?? null);

	/**
	 * What the toolbar acts on. Nothing ticked means the page on the stage rather than every
	 * page: the buttons sit under that page, Crop and Rescan have always read it that way,
	 * and a rotation of the whole scan is worth asking for — the toolbar's All does that.
	 */
	const targets = $derived(selection.size > 0 ? selection : new Set(shown ? [shown.Index] : []));

	const anyDeleted = $derived(
		session?.Pages.filter((p) => targets.has(p.Index)).some((p) => p.Deleted) ?? false
	);
	/** The other half of it: what Delete would still have to work on. */
	const anyActive = $derived(
		session?.Pages.filter((p) => targets.has(p.Index)).some((p) => !p.Deleted) ?? false
	);
	const filters = $derived(
		new Set((session?.Pages ?? []).filter((p) => !p.Deleted).map((p) => p.Filter))
	);
	const filterLabel = $derived(
		filters.size > 1 ? 'Mixed' : pageFilterLabels[[...filters][0] ?? PageFilter.Original]
	);
	/**
	 * What the Scan section says while it is shut. Worded exactly as the line it replaces, which
	 * sat under the export buttons: it is a fact about the paper, not about the saving.
	 */
	const pageSummary = $derived(
		session
			? `${activePages} of ${session.Pages.length} page${session.Pages.length === 1 ? '' : 's'}`
			: ''
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

	async function stop() {
		await client.hub.stopScan(id);
	}

	/**
	 * Discard marks the scan discarded and drops its pages, but leaves the row in
	 * past scans. Deleting a session outright is the trash button over there.
	 */
	async function discard() {
		try {
			await client.hub.discardSession(id);
			cameFrom.leaving(id);
			await goto(resolve('/'));
			toast.show('Scan discarded');
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

	/** Export: the server writes the document into the folder settings point at. */
	async function save() {
		error = null;
		exporting = true;
		try {
			const result = await client.hub.export(id, {
				Mode: ExportMode.Save,
				Format: $exportPrefs.format,
				Name: exportName,
				Directory: $exportPrefs.directory.trim() || undefined
			});
			savedPath = savedLabel(result);
			const count = result.Documents.length;
			toast.show(count === 1 ? 'Saved' : `Saved ${count} documents`);
		} catch (e) {
			error = hubError(e);
		} finally {
			exporting = false;
		}
	}

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
				Mode: ExportMode.Download,
				Format: $exportPrefs.format,
				Name: exportName
			});
			const count = target
				? await saveToDisk(result, target, base, extension)
				: saveToBrowser(result);
			const verb = target ? 'Saved' : 'Downloaded';
			toast.show(count === 1 ? verb : `${verb} ${count} files`);
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
			toast.show(`${after.Pages.length - before} pages added`);
		} catch (e) {
			error = hubError(e);
		} finally {
			merged = false;
		}
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

<svelte:head>
	<title>{session ? formatTime(session.CreatedAt) : 'Session'} · YASS</title>
</svelte:head>

{#snippet heading(title: string, value = '', testid = '')}
	<div class="flex items-baseline gap-2">
		<span class="text-xs font-bold tracking-[0.06em] text-ink-muted uppercase">{title}</span>
		{#if value}
			<span
				class="ml-auto min-w-0 truncate text-[13px] font-semibold text-ink-muted"
				data-testid={testid || undefined}>{value}</span
			>
		{/if}
	</div>
{/snippet}

{#snippet fact(label: string, value: string)}
	<!-- A row rather than a tile: two words and a figure do not need a card each, and a
	     250px column has no room for the pair of them side by side. -->
	<div class="flex items-baseline justify-between gap-2 py-0.5">
		<span class="flex-none text-xs text-ink-muted">{label}</span>
		<span class="truncate text-[13px] font-bold tracking-[-0.01em] tabular-nums">{value}</span>
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
			ondelete={() => {
				// A snapshot, because undo has to put back the pages this click took and not
				// whatever happens to be on the stage by the time the toast is pressed.
				const deleting = new Set(targets);
				send(deleteEdit(deleting));
				toast.show(`${deleting.size} page${deleting.size === 1 ? '' : 's'} deleted`, () =>
					send(restoreEdit(deleting))
				);
			}}
			onrestore={() => send(restoreEdit(targets))}
			onfilter={(filter) => send(setFilterEdit(filter, targets))}
			onexpand={() => (previewIndex = targetPage())}
			oncrop={() => (cropping = targetPage())}
			onrescan={rescan}
			{scanning}
			onclear={() => (selection = clear())}
			onselectall={selectAll}
		/>
	{/if}
{/snippet}

<!--
	Side by side, the three panes get exactly the height left over after the header, the
	footer and this container's own padding -- so the review screen fits without scrolling
	and each pane scrolls its own overflow instead. Stacked (below lg) they keep their
	natural height and the page scrolls, which is the right behaviour on a phone.
-->
<div
	class="mx-auto flex max-w-[2200px] animate-slide-from-right flex-col px-4 pt-2 pb-7 sm:px-7 lg:h-[calc(100dvh-var(--app-header-h)-var(--app-footer-h))] lg:min-h-0"
>
	{#if missing}
		<p class="rounded-[14px] bg-danger-bg px-4 py-3 font-semibold text-danger" role="alert">
			This session no longer exists.
		</p>
	{:else if !session}
		<p class="text-ink-muted">Loading…</p>
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
			<nav class="order-2 w-full flex-none lg:order-1 lg:min-h-0 lg:w-auto" aria-label="Pages">
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
					Stop sits in a slot of its own rather than after the pills: the pills change
					width as pages arrive, and a Stop button that slides around mid-scan is both
					hard to hit and hard to test.

					The row holds the state chip and Stop, and neither exists once a scan is in
					review — so the row goes too, rather than leaving a gap above the page.
				-->
				{#if session.State !== SessionState.Review}
					<div
						class="relative flex w-full flex-wrap items-center justify-center gap-2 {scanning
							? 'pr-24'
							: ''}"
					>
						<!--
							Every state but Review says something the screen does not: pages are still
							coming, the save is running, it failed, it is saved. That is the same
							condition the row itself is under, so the chip needs no test of its own.
						-->
						<span
							class="rounded-full bg-surface px-3 py-1.5 text-[13px] font-bold shadow-chip"
							data-testid="session-state"
						>
							{sessionStateLabel(session.State)}
						</span>
						{#if scanning}
							<button
								type="button"
								class="absolute top-1/2 right-0 flex h-[30px] -translate-y-1/2 items-center gap-1.5 rounded-full bg-surface px-3 text-[13px] font-bold text-ink shadow-chip transition-colors hover:bg-danger-bg hover:text-danger"
								onclick={stop}
								data-testid="stop-button"
							>
								<Icon name="stop" size={14} />
								Stop
							</button>
						{/if}
					</div>
				{/if}

				{#if shown}
					<div class="flex w-full items-center justify-center gap-2 lg:min-h-0 lg:flex-1">
						<button
							type="button"
							class="flex h-11 w-11 flex-none items-center justify-center rounded-full bg-surface shadow-nav transition-[background-color,transform] duration-150 hover:scale-105 hover:bg-accent-wash disabled:pointer-events-none disabled:opacity-35"
							onclick={() => step(-1)}
							disabled={current === 0}
							aria-label="Previous page"
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
							class="w-full max-w-[max(280px,calc((100dvh-230px)/1.414))] flex-1 lg:h-full lg:w-auto lg:max-w-none lg:flex-none"
							style="aspect-ratio: 1 / 1.414"
						>
							<img
								src={imageUrl(id, shown.Index, shown.Hash, 'preview')}
								alt="Page {shown.Index + 1}"
								class="h-full w-full rounded-[6px] object-contain drop-shadow-[0_18px_36px_rgb(19_26_43_/_0.14)]"
							/>
						</div>

						<button
							type="button"
							class="flex h-11 w-11 flex-none items-center justify-center rounded-full bg-surface shadow-nav transition-[background-color,transform] duration-150 hover:scale-105 hover:bg-accent-wash disabled:pointer-events-none disabled:opacity-35"
							onclick={() => step(1)}
							disabled={current >= session.Pages.length - 1}
							aria-label="Next page"
						>
							<Icon name="chevronRight" />
						</button>
					</div>
				{:else}
					<div
						class="flex w-full max-w-[280px] items-center justify-center rounded-[10px] border-[1.5px] border-dashed border-accent-dash text-sm text-ink-muted"
						style="aspect-ratio: 1 / 1.414"
					>
						{scanning ? 'Waiting for the first page…' : 'No pages'}
					</div>
				{/if}

				{@render pageTools(false)}

				{#if session.Pages.length > 0}
					<!--
						Last of all, under the tools: it is the quietest thing on the stage and has no
						business between the page and the buttons that edit it. As few characters as
						say it, too.
					-->
					<p
						class="text-[13px] font-semibold text-ink-muted tabular-nums"
						data-testid="page-position"
					>
						{current + 1} / {session.Pages.length}
					</p>
				{/if}
			</main>

			<!--
				Width kept close to the filmstrip's: the stage sits between the two, so the page
				on screen is only centred while the columns either side of it match. Neither can
				be the other's exact width — thumbnails do not want 280px and this card does not
				want 180 — so they meet in the middle and the page lands near enough to centre.
			-->
			<aside
				class="order-3 flex w-full min-w-0 basis-full flex-col gap-3.5 rounded-[24px] bg-surface p-[22px] shadow-panel lg:min-h-0 lg:w-auto lg:basis-[clamp(250px,16vw,280px)] lg:overflow-y-auto"
			>
				{@render heading('Name')}

				<NameField
					value={displayName}
					placeholder={exportBaseName(session, $exportPrefs.nameTemplate)}
					extension={fileExtensions[$exportPrefs.format]}
					onrename={rename}
				/>

				{@render heading('Save')}

				<ExportBar
					{session}
					progress={$exportProgress[id] ?? null}
					{allowDownload}
					busy={exporting}
					{savedPath}
					exportDirectory={$exportPrefs.directory.trim() || exportRoots[0] || ''}
					exportEnabled={$exportPrefs.exportButton}
					picker={savePicker() !== null}
					onsave={save}
					ondownload={saveAs}
				/>

				<!-- The facts about the scan, with the page count up on the heading beside them. -->
				<div class="flex flex-col gap-1" data-testid="scan-section">
					{@render heading('Scan', pageSummary, 'page-count')}
					{@render fact('Filter', filterLabel)}
					<!-- The scanned pages on disk, not an estimate of the PDF the mockup guesses at. -->
					{@render fact('Size', formatBytes(session.SizeBytes))}
				</div>

				<div class="flex flex-col gap-0.5" data-testid="more-section">
					{@render heading('More')}
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
						Scan more pages
					</button>
					<button
						type="button"
						class={ghost}
						onclick={() => (merging = true)}
						disabled={!editable || mergeable.length === 0}
						title={!editable
							? busyReason
							: mergeable.length === 0
								? 'No other scan has pages to add'
								: undefined}
						data-testid="merge-button"
					>
						<Icon name="layers" />
						Add pages from past scan
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
						{confirmDiscard ? 'Really discard?' : 'Discard scan'}
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

{#if cropping !== null && session?.Pages[cropping]}
	<CropModal
		src={imageUrl(id, session.Pages[cropping].Index, session.Pages[cropping].Hash, 'preview')}
		alt="Page {session.Pages[cropping].Index + 1}"
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
	/>
{/if}
