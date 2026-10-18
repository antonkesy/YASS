<script lang="ts">
	import { untrack } from 'svelte';
	import { page as route } from '$app/state';
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { toast } from '$lib/toast';
	import { t } from '$lib/i18n/index.svelte';
	import { cameFrom } from '$lib/navigation';
	import { getHub, imageUrl, sortedSessions } from '$lib/hub/hub';
	import { SessionState } from '$lib/hub/generated/Yass.Core.Domain';
	import type { CropBox, PageAdjust, PageEdit } from '$lib/hub/generated/Yass.Core.Domain';
	import { formatTime, sessionStateLabel } from '$lib/format';
	import {
		addCleanupEdit,
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
	import Alert from '$lib/components/Alert.svelte';
	import PageGrid from '$lib/components/PageGrid.svelte';
	import ReviewStage from '$lib/components/ReviewStage.svelte';
	import PageOverview from '$lib/components/PageOverview.svelte';
	import Toolbar from '$lib/components/Toolbar.svelte';
	import Preview from '$lib/components/Preview.svelte';
	import ReviewSidebar from '$lib/components/ReviewSidebar.svelte';
	import CropModal from '$lib/components/CropModal.svelte';
	import MergeModal from '$lib/components/MergeModal.svelte';
	import StopScanButton from '$lib/components/StopScanButton.svelte';
	import { canSave } from '$lib/review/download';
	import { Review } from '$lib/review/review.svelte';
	import { exportPrefs, nameTemplate, reviewPrefs } from '$lib/presets';

	const client = getHub();
	const sessions = client.sessions;
	const connection = client.connection;

	const id = $derived(route.params.id ?? '');
	const session = $derived($sessions.get(id) ?? null);
	const review = new Review(
		() => id,
		() => session
	);
	const send = (edit: PageEdit) => review.send(edit);
	let missing = $state(false);
	let selection = $state<Set<number>>(clear());
	let current = $state(0);
	let overview = $state(false);

	let dialog = $state<
		| { kind: 'merge' }
		| { kind: 'adjust'; pages: ReadonlySet<number>; from: number }
		| { kind: 'crop' | 'straighten'; at: number }
		| null
	>(null);
	let previewAt = $state<number | null>(null);
	const previewIndex = $derived(
		previewAt !== null && session && previewAt >= session.Pages.length ? null : previewAt
	);

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
	const shown = $derived(session?.Pages[current] ?? null);

	// A view transition already slides the screen in; its own entrance would be a second slide.
	const entrance = screenTransition.active ? '' : 'animate-slide-from-right';

	// Prefetch around the visible page; held off during a scan, when render slots belong to thumbnails.
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

	const targets = $derived(selection.size > 0 ? selection : new Set(shown ? [shown.Index] : []));
	const targetCleanup = $derived(
		commonCleanup(session?.Pages.filter((p) => targets.has(p.Index)) ?? [])
	);

	const anyDeleted = $derived(
		session?.Pages.filter((p) => targets.has(p.Index)).some((p) => p.Deleted) ?? false
	);
	const anyActive = $derived(
		session?.Pages.filter((p) => targets.has(p.Index)).some((p) => !p.Deleted) ?? false
	);

	const mergeable = $derived(
		sortedSessions($sessions).filter((s) => s.Id !== id && s.Pages.some((p) => !p.Deleted))
	);

	function show(index: number) {
		const at = session?.Pages.findIndex((p) => p.Index === index) ?? -1;
		if (at >= 0) current = at;
	}

	function select(index: number) {
		selection = toggle(selection, index);
	}

	// A reorder renumbers every page, so the ticks would land on pages nobody picked.
	function reorder(order: number[]) {
		selection = clear();
		send(reorderEdit(order));
	}

	function selectAll() {
		selection = new Set((session?.Pages ?? []).map((p) => p.Index));
	}

	function closeOverview() {
		overview = false;
		selection = clear();
	}

	function deletePages() {
		// Snapshot: undo must restore these pages, not whatever is on the stage by then.
		const deleting = new Set(targets);
		send(deleteEdit(deleting));
		toast.show(t('review.pagesDeleted', { n: deleting.size }), () => send(restoreEdit(deleting)));
	}

	const screen = useLayer(() => {
		cameFrom.leaving(id);
		void goto(resolve('/'));
	});

	function keydown(e: KeyboardEvent) {
		if (!session || !screen()?.top || typing(e)) return;
		if ((e.ctrlKey || e.metaKey) && !e.altKey && e.key.toLowerCase() === 's') {
			e.preventDefault();
			if (canSave(session, review.exporting)) void review.saveAs();
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
				if (last < 0 || (e.target as HTMLElement).closest('button, a, [role]')) return;
				previewAt = targetPage();
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

	function merge(sourceId: string) {
		dialog = null;
		void review.merge(sourceId);
	}

	function rescan() {
		const page = session?.Pages[targetPage()];
		if (page) void review.rescan(page.Index);
	}

	function applyAdjust(adjust: PageAdjust | undefined) {
		if (dialog?.kind !== 'adjust') return;
		const pages = dialog.pages;
		dialog = null;
		send(setAdjustEdit(adjust, pages));
	}

	function applySkew(degrees: number) {
		const page = dialog?.kind === 'straighten' ? session?.Pages[dialog.at] : null;
		dialog = null;
		if (page) send(setSkewEdit(degrees, new Set([page.Index])));
	}

	function applyCrop(crop: CropBox | undefined) {
		const page = dialog?.kind === 'crop' ? session?.Pages[dialog.at] : null;
		dialog = null;
		if (page) send(setCropEdit(crop, new Set([page.Index])));
	}

	function step(delta: number) {
		const count = session?.Pages.length ?? 0;
		current = Math.min(count - 1, Math.max(0, current + delta));
	}
</script>

<svelte:window onkeydown={keydown} />

<svelte:head>
	<title>{session ? formatTime(session.CreatedAt) : t('review.sessionTitle')} · YASS</title>
</svelte:head>

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
			onexpand={() => (previewAt = targetPage())}
			oncrop={() => (dialog = { kind: 'crop', at: targetPage() })}
			onstraighten={() => (dialog = { kind: 'straighten', at: targetPage() })}
			onautocrop={() => review.autoCrop(targets)}
			autocropping={review.autocropping}
			onadjust={() => (dialog = { kind: 'adjust', pages: new Set(targets), from: targetPage() })}
			onrescan={rescan}
			{scanning}
			onclear={() => (selection = clear())}
			onselectall={selectAll}
		/>
	{/if}
{/snippet}

<!-- From lg the panes get exactly the height under the header and scroll on their own. -->
<div
	class="flex {entrance} flex-col px-4 pt-2 pb-7 sm:px-7 lg:h-[calc(100dvh-var(--app-header-h))] lg:min-h-0"
>
	{#if missing}
		<Alert tall>{t('review.sessionGone')}</Alert>
	{:else if !session}
		<p class="text-ink-muted">{t('review.loading')}</p>
	{:else}
		{#if session.Error}
			<Alert tone="warning" class="mb-3">{session.Error}</Alert>
		{/if}
		{#if review.error}
			<Alert class="mb-3">{review.error}</Alert>
		{/if}

		<div class="flex min-h-0 flex-wrap items-start gap-5 lg:flex-1 lg:flex-nowrap lg:items-stretch">
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
					onopen={(i) => (previewAt = i)}
					onscanmore={editable ? () => review.scanMore() : undefined}
					reorderable={editable}
					onreorder={reorder}
					onexpand={() => (overview = true)}
				/>
			</nav>

			<main
				class="order-1 flex w-full flex-1 basis-full flex-col items-center gap-[18px] lg:order-2 lg:min-h-0 lg:basis-[380px]"
			>
				{#if scanning}
					<div class="w-full max-w-[420px] lg:hidden">
						<StopScanButton
							onstop={() => review.stop()}
							stopping={review.stopping}
							testid="stop-scan-top"
						/>
					</div>
				{/if}
				{#if session.State !== SessionState.Review && !scanning}
					<div class="flex w-full flex-wrap items-center justify-center gap-2">
						<span
							class="rounded-full bg-surface px-3 py-1.5 text-[13px] font-bold shadow-chip"
							data-testid="session-state"
						>
							{sessionStateLabel(session)}
						</span>
					</div>
				{/if}

				<ReviewStage sessionId={id} pages={session.Pages} {current} {scanning} onstep={step} />

				{@render pageTools(false)}
			</main>

			<ReviewSidebar
				{session}
				{review}
				mergeable={mergeable.length}
				onmerge={() => (dialog = { kind: 'merge' })}
			/>
		</div>
	{/if}
</div>

{#if dialog?.kind === 'merge'}
	<MergeModal
		sessions={mergeable}
		template={nameTemplate($exportPrefs)}
		onpick={merge}
		oncancel={() => (dialog = null)}
	/>
{/if}

{#if dialog?.kind === 'adjust' && session?.Pages[dialog.from]}
	{@const from = session.Pages[dialog.from]}
	<AdjustModal
		src={imageUrl(id, from.Index, from.Hash, 'preview')}
		alt={t('review.pageN', { n: from.Index + 1 })}
		adjust={from.Adjust ?? null}
		count={dialog.pages.size}
		onapply={applyAdjust}
		oncancel={() => (dialog = null)}
	/>
{/if}

{#if dialog?.kind === 'straighten' && session?.Pages[dialog.at]}
	{@const page = session.Pages[dialog.at]}
	<StraightenModal
		src={imageUrl(id, page.Index, page.Hash, 'preview')}
		alt={t('review.pageN', { n: page.Index + 1 })}
		skew={page.Skew}
		onapply={applySkew}
		oncancel={() => (dialog = null)}
	/>
{/if}

{#if dialog?.kind === 'crop' && session?.Pages[dialog.at]}
	{@const page = session.Pages[dialog.at]}
	<CropModal
		src={imageUrl(id, page.Index, page.Hash, 'frame')}
		rotation={page.Rotation}
		alt={t('review.pageN', { n: page.Index + 1 })}
		crop={page.Crop ?? null}
		onapply={applyCrop}
		oncancel={() => (dialog = null)}
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
		onopen={(i) => (previewAt = i)}
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
		onnavigate={(i) => (previewAt = i)}
		onclose={() => (previewAt = null)}
		onrotate={(i, deg) => send(rotateEdit(deg, new Set([i])))}
		editable={editable && !scanning}
		paused={dialog !== null}
		onadjust={(i) => (dialog = { kind: 'adjust', pages: new Set([i]), from: i })}
		oncrop={(i) => (dialog = { kind: 'crop', at: i })}
		onstraighten={(i) => (dialog = { kind: 'straighten', at: i })}
		onfilter={(i, filter) => send(setFilterEdit(filter, new Set([i])))}
		oncleanup={(i, cleanup, on) =>
			send(on ? addCleanupEdit(cleanup, new Set([i])) : removeCleanupEdit(cleanup, new Set([i])))}
	/>
{/if}
