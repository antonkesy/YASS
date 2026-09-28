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
	import { activePageCount, formatBytes, formatTime, sessionStateLabel } from '$lib/format';
	import {
		applyOptimistic,
		deleteEdit,
		reorderEdit,
		restoreEdit,
		rotateEdit,
		setCropEdit,
		setFilterEdit,
		setSplitEdit,
		documentNumbers,
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
	import NameSuggestions from '$lib/components/NameSuggestions.svelte';
	import { saveToBrowser, savedLabel } from '$lib/review/download';
	import { exportPrefs, rememberFolder, reviewPrefs } from '$lib/presets';
	import { ExportMode, type NameSuggestionsDto } from '$lib/hub/generated/Yass.Core.Hub';

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
	let suggesting = $state<'reading' | 'thinking' | 'ready' | null>(null);
	let suggestions = $state<NameSuggestionsDto | null>(null);
	/** Whether the server can read page text at all, which hides Find name when it cannot. */
	let canSuggest = $state(false);

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
	const anyDeleted = $derived(
		(selection.size === 0
			? session?.Pages
			: session?.Pages.filter((p) => selection.has(p.Index))
		)?.some((p) => p.Deleted) ?? false
	);

	const shown = $derived(session?.Pages[current] ?? null);
	const filters = $derived(
		new Set((session?.Pages ?? []).filter((p) => !p.Deleted).map((p) => p.Filter))
	);
	const filterLabel = $derived(
		filters.size > 1 ? 'Mixed' : pageFilterLabels[[...filters][0] ?? PageFilter.Original]
	);
	const documents = $derived(documentNumbers(session?.Pages ?? []));
	const documentCount = $derived(Math.max(1, ...[...documents.values()], 1));

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

	async function rename(name: string) {
		try {
			await client.hub.renameSession(id, name);
		} catch (e) {
			error = hubError(e);
		}
	}

	async function exportAs(mode: ExportMode, directory: string | null = null) {
		error = null;
		exporting = true;
		const target = (directory ?? $exportPrefs.directory).trim();
		try {
			const result = await client.hub.export(id, {
				Mode: mode,
				Format: $exportPrefs.format,
				// The name field wins; the template is what an unnamed scan falls back to.
				Name: session?.Name?.trim() || $exportPrefs.nameTemplate.trim() || undefined,
				Directory: mode === ExportMode.Save ? target || undefined : undefined
			});
			// Only once the server has accepted it: an offer to save somewhere it refuses
			// is worse than no offer at all.
			if (directory) exportPrefs.set(rememberFolder($exportPrefs, directory));
			if (mode === ExportMode.Save) {
				savedPath = savedLabel(result);
				const count = result.Documents.length;
				toast.show(count === 1 ? 'Saved' : `Saved ${count} documents`);
			} else {
				const started = saveToBrowser(result);
				toast.show(started === 1 ? 'Downloaded' : `Downloaded ${started} files`);
			}
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

	// One probe on connect tells us whether the Find name button is worth showing.
	$effect(() => {
		if ($connection === 'connected' && !canSuggest && session && session.Pages.length > 0) {
			client.hub
				.suggestNames(id)
				.then((r) => (canSuggest = r.CanRead))
				.catch(() => undefined);
		}
	});

	async function findName() {
		suggesting = 'reading';
		suggestions = null;
		error = null;
		try {
			// The two phases are the server's own: read the page, then ask the model.
			const pending = client.hub.suggestNames(id);
			setTimeout(() => {
				if (suggesting === 'reading') suggesting = 'thinking';
			}, 650);
			suggestions = await pending;
			suggesting = 'ready';
		} catch (e) {
			error = hubError(e);
			suggesting = null;
		}
	}

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

<div class="mx-auto flex max-w-[2200px] animate-slide-from-right flex-col px-4 pt-2 pb-7 sm:px-7">
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

		<div class="flex flex-wrap items-start gap-5">
			<!-- Filmstrip: down the left on desktop, above the page on a phone. -->
			<nav class="order-2 w-full flex-none lg:order-1 lg:w-auto" aria-label="Pages">
				<PageGrid
					sessionId={id}
					pages={session.Pages}
					{selection}
					current={session.Pages[current]?.Index ?? -1}
					{scanning}
					onshow={show}
					ontoggle={select}
					onopen={(i) => (previewIndex = i)}
					{documents}
					onscanmore={editable ? scanMore : undefined}
					reorderable={editable}
					onreorder={reorder}
					onexpand={() => (overview = true)}
				/>
			</nav>

			<main
				class="order-1 flex w-full flex-1 basis-full flex-col items-center gap-[18px] lg:order-2 lg:basis-[380px]"
			>
				<!--
					Stop sits in a slot of its own rather than after the pills: the pills change
					width as pages arrive, and a Stop button that slides around mid-scan is both
					hard to hit and hard to test.
				-->
				<div
					class="relative flex w-full flex-wrap items-center justify-center gap-2 {scanning
						? 'pr-24'
						: ''}"
				>
					<span
						class="rounded-full bg-surface px-3 py-1.5 text-[13px] font-bold shadow-chip"
						data-testid="session-state"
					>
						{sessionStateLabel(session.State)}
					</span>
					{#if session.Pages.length > 0}
						<span
							class="rounded-full bg-surface px-3 py-1.5 text-[13px] font-bold tabular-nums shadow-chip"
						>
							Page {current + 1} of {session.Pages.length}
						</span>
					{/if}
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

				{#if shown}
					<div class="flex w-full items-center justify-center gap-2">
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
						<div
							bind:this={stage}
							class="w-full max-w-[max(280px,calc((100dvh-230px)/1.414))] flex-1"
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

				<Toolbar
					selectedCount={selection.size}
					total={session.Pages.length}
					{anyDeleted}
					disabled={!editable}
					onrotate={(deg) => send(rotateEdit(deg, selection))}
					ondelete={() => {
						const targets = new Set(selection);
						const count = targets.size === 0 ? session.Pages.length : targets.size;
						send(deleteEdit(targets));
						toast.show(`${count} page${count === 1 ? '' : 's'} deleted`, () =>
							send(restoreEdit(targets))
						);
					}}
					onrestore={() => send(restoreEdit(selection))}
					onfilter={(filter) => send(setFilterEdit(filter, selection))}
					oncrop={() => (cropping = targetPage())}
					onrescan={rescan}
					onsplit={(split) => {
						const page = session?.Pages[targetPage()];
						if (page) send(setSplitEdit(split, new Set([page.Index])));
					}}
					splits={session.Pages[targetPage()]?.StartsDocument ?? false}
					firstPage={targetPage() === 0}
					{scanning}
					onclear={() => (selection = clear())}
				/>
			</main>

			<aside
				class="order-3 flex w-full min-w-0 basis-full flex-col gap-3.5 rounded-[24px] bg-surface p-[22px] shadow-panel lg:sticky lg:top-[72px] lg:w-auto lg:basis-[clamp(300px,22vw,380px)]"
			>
				<div class="flex items-center gap-2">
					<p class="flex-1 text-xs font-bold tracking-[0.06em] text-ink-muted uppercase">
						Document name
					</p>
					{#if canSuggest}
						<button
							type="button"
							class="flex h-[30px] items-center gap-1.5 rounded-full px-2.5 text-xs font-bold transition-transform duration-150 hover:-translate-y-px disabled:pointer-events-none disabled:opacity-45 {suggesting
								? 'bg-ai text-white'
								: 'bg-ai-bg text-ai-ink'}"
							onclick={findName}
							disabled={suggesting !== null && suggesting !== 'ready'}
							data-testid="suggest-button"
						>
							<Icon name="sparkle" />
							Find name
						</button>
					{/if}
				</div>

				<div class="relative">
					<NameField
						value={session.Name ?? null}
						placeholder={formatTime(session.CreatedAt)}
						onrename={rename}
					/>

					{#if suggesting}
						<NameSuggestions
							state={suggesting === 'ready' ? 'ready' : suggesting}
							result={suggestions}
							onpick={(name) => {
								suggesting = null;
								rename(name);
							}}
							onretry={findName}
							onclose={() => (suggesting = null)}
						/>
					{/if}
				</div>

				<p class="text-xs font-bold tracking-[0.06em] text-ink-muted uppercase">Document</p>
				<div class="grid grid-cols-2 gap-2">
					<div class="rounded-[14px] bg-app p-3">
						<p class="text-xs text-ink-muted">Pages</p>
						<p class="text-[18px] font-extrabold tracking-[-0.01em] tabular-nums">
							{activePageCount(session)}
						</p>
					</div>
					<div class="rounded-[14px] bg-app p-3">
						<p class="text-xs text-ink-muted">Documents</p>
						<p class="text-[18px] font-extrabold tracking-[-0.01em] tabular-nums">
							{documentCount}
						</p>
					</div>
					<div class="rounded-[14px] bg-app p-3">
						<p class="text-xs text-ink-muted">Filter</p>
						<p class="truncate text-[18px] font-extrabold tracking-[-0.01em]">{filterLabel}</p>
					</div>
					<div class="rounded-[14px] bg-app p-3">
						<!-- The scanned pages on disk, not an estimate of the PDF the mockup guesses at. -->
						<p class="text-xs text-ink-muted">Size</p>
						<p class="text-[18px] font-extrabold tracking-[-0.01em] tabular-nums">
							{formatBytes(session.SizeBytes)}
						</p>
					</div>
				</div>

				<ExportBar
					{session}
					progress={$exportProgress[id] ?? null}
					{allowDownload}
					busy={exporting}
					{savedPath}
					exportDirectory={$exportPrefs.directory.trim() || exportRoots[0] || ''}
					exportEnabled={$exportPrefs.exportButton}
					roots={exportRoots}
					recent={$exportPrefs.recentFolders}
					onsave={(directory) => exportAs(ExportMode.Save, directory)}
					ondownload={() => exportAs(ExportMode.Download)}
				/>

				<div class="flex flex-col gap-0.5 border-t border-line pt-2">
					<button
						type="button"
						class={ghost}
						onclick={scanMore}
						disabled={!editable}
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
						data-testid="merge-button"
					>
						<Icon name="layers" />
						Add pages from past scan
					</button>
					<button
						type="button"
						class="{ghost} text-danger hover:bg-danger-bg"
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
		{documents}
		reorderable={editable}
		tile={$reviewPrefs.overviewTile}
		onzoom={(overviewTile) => reviewPrefs.set({ ...$reviewPrefs, overviewTile })}
		onshow={show}
		ontoggle={select}
		onopen={(i) => (previewIndex = i)}
		onreorder={reorder}
		onselectall={selectAll}
		onclear={() => (selection = clear())}
		onclose={() => (overview = false)}
	/>
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
