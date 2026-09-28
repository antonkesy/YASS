<script lang="ts">
	import { untrack } from 'svelte';
	import { page as route } from '$app/state';
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { getHub, imageUrl } from '$lib/hub/hub';
	import { SessionState, type ColorMode } from '$lib/hub/generated/Yass.Core.Domain';
	import type { PageEdit } from '$lib/hub/generated/Yass.Core.Domain';
	import { activePageCount, colorLabel, formatTime, sessionStateLabel } from '$lib/format';
	import {
		applyOptimistic,
		deleteEdit,
		reorderEdit,
		restoreEdit,
		rotateEdit,
		setColorEdit
	} from '$lib/review/edits';
	import { clear, prune, toggle } from '$lib/review/selection';
	import PageGrid from '$lib/components/PageGrid.svelte';
	import Toolbar from '$lib/components/Toolbar.svelte';
	import Preview from '$lib/components/Preview.svelte';
	import ExportBar from '$lib/components/ExportBar.svelte';
	import { Icon } from '$lib/icons';
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
	/** Position in `pages` shown in the centre column. */
	let current = $state(0);

	$effect(() => {
		if ($connection === 'connected') {
			client.hub
				.getEffectiveOptions()
				.then((o) => (allowDownload = o.AllowDownload ?? false))
				.catch(() => undefined);
		}
	});
	let previewIndex = $state<number | null>(null);

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
		new Set((session?.Pages ?? []).filter((p) => !p.Deleted).map((p) => p.ColorOverride))
	);
	const filterLabel = $derived(
		filters.size > 1
			? 'Mixed'
			: colorLabel([...filters][0] ?? session?.OptionsUsed.Color) || 'Scan default'
	);

	async function send(edit: PageEdit) {
		if (!session) return;
		error = null;
		client.updateLocal(id, (s) => applyOptimistic(s, edit));
		try {
			await client.hub.applyEdits(id, [edit]);
		} catch (e) {
			error = e instanceof Error ? e.message : String(e);
			await client.refresh(id);
		}
	}

	async function stop() {
		await client.hub.stopScan(id);
	}

	async function discard() {
		try {
			await client.hub.deleteSession(id);
			await goto(resolve('/'));
		} catch (e) {
			error = e instanceof Error ? e.message : String(e);
		}
	}

	async function exportAs(mode: ExportMode) {
		error = null;
		exporting = true;
		try {
			const result = await client.hub.export(id, { Mode: mode });
			if (mode === ExportMode.Save) {
				savedPath = result.SavedPath ?? '';
			} else if (result.DownloadPath) {
				const a = document.createElement('a');
				a.href = result.DownloadPath;
				a.download = '';
				a.rel = 'noopener';
				document.body.append(a);
				a.click();
				a.remove();
			}
		} catch (e) {
			error = e instanceof Error ? e.message : String(e);
		} finally {
			exporting = false;
		}
	}

	function select(index: number) {
		const at = session?.Pages.findIndex((p) => p.Index === index) ?? -1;
		if (at >= 0) current = at;
		selection = toggle(selection, index);
	}

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

<!--
	Fades rather than slides in. The mockup slides this screen across, but a half-second
	transform on the whole surface moves every control while it runs, and Stop has to be
	hittable immediately — a short feeder run is over before the slide finishes.
-->
<div class="mx-auto flex max-w-[2200px] animate-fade-in flex-col px-4 pt-2 pb-7 sm:px-7">
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
					{scanning}
					ontoggle={select}
					onopen={(i) => (previewIndex = i)}
					reorderable={editable}
					onreorder={(order) => {
						selection = clear();
						send(reorderEdit(order));
					}}
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
					ondelete={() => send(deleteEdit(selection))}
					onrestore={() => send(restoreEdit(selection))}
					oncolor={(mode: ColorMode | undefined) => send(setColorEdit(mode, selection))}
					onclear={() => (selection = clear())}
				/>
			</main>

			<aside
				class="order-3 flex w-full basis-full flex-col gap-3.5 rounded-[24px] bg-surface p-[22px] shadow-panel lg:sticky lg:top-[72px] lg:w-auto lg:basis-[clamp(300px,22vw,380px)]"
			>
				<h1 class="text-[19px] font-extrabold tracking-[-0.01em]">
					{formatTime(session.CreatedAt)}
				</h1>

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
						<p class="text-[18px] font-extrabold tracking-[-0.01em] tabular-nums">1</p>
					</div>
					<div class="col-span-2 rounded-[14px] bg-app p-3">
						<p class="text-xs text-ink-muted">Filter</p>
						<p class="text-[18px] font-extrabold tracking-[-0.01em]">{filterLabel}</p>
					</div>
				</div>

				<ExportBar
					{session}
					progress={$exportProgress[id] ?? null}
					{allowDownload}
					busy={exporting}
					{savedPath}
					onsave={() => exportAs(ExportMode.Save)}
					ondownload={() => exportAs(ExportMode.Download)}
					ondiscard={discard}
				/>
			</aside>
		</div>
	{/if}
</div>

<!-- Outside the container above: an animated ancestor would trap this overlay under the header. -->
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
