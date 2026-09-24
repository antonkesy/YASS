<script lang="ts">
	import { untrack } from 'svelte';
	import { page as route } from '$app/state';
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { getHub } from '$lib/hub/hub';
	import { SessionState, type ColorMode } from '$lib/hub/generated/Yawss.Core.Domain';
	import type { PageEdit } from '$lib/hub/generated/Yawss.Core.Domain';
	import { formatTime, sessionStateLabel } from '$lib/format';
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
	import { ExportMode } from '$lib/hub/generated/Yawss.Core.Hub';

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
		const current = untrack(() => selection);
		const pruned = prune(current, count);
		if (pruned.size !== current.size) selection = pruned;
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

	function open(index: number) {
		previewIndex = index;
	}

	$effect(() => {
		if (previewIndex !== null && session && previewIndex >= session.Pages.length)
			previewIndex = null;
	});
</script>

<svelte:head
	><title>{session ? formatTime(session.CreatedAt) : 'Session'} · YAWSS</title></svelte:head
>

<div class="mx-auto flex min-h-dvh max-w-6xl flex-col px-3 pt-3 pb-28">
	<header class="mb-3 flex items-center gap-2">
		<a
			href={resolve('/')}
			class="flex min-h-11 min-w-11 items-center justify-center rounded-full text-stone-700 hover:bg-stone-200"
			aria-label="Back to home"
		>
			<svg
				class="h-6 w-6"
				viewBox="0 0 24 24"
				fill="none"
				stroke="currentColor"
				stroke-width="2"
				aria-hidden="true"><path d="M15 6l-6 6 6 6" /></svg
			>
		</a>
		{#if session}
			<div class="flex flex-col">
				<h1 class="font-semibold">{formatTime(session.CreatedAt)}</h1>
				<span class="text-sm text-stone-600" data-testid="session-state"
					>{sessionStateLabel(session.State)}</span
				>
			</div>
			{#if scanning}
				<button
					type="button"
					class="ml-auto min-h-11 rounded-lg bg-rose-600 px-4 font-medium text-white hover:bg-rose-500"
					onclick={stop}
					data-testid="stop-button"
				>
					Stop
				</button>
			{/if}
		{/if}
	</header>

	{#if missing}
		<p class="rounded-lg bg-rose-100 px-4 py-3 text-rose-800" role="alert">
			This session no longer exists.
		</p>
	{:else if !session}
		<p class="text-stone-500">Loading…</p>
	{:else}
		{#if session.Error}
			<p class="mb-3 rounded-lg bg-amber-100 px-4 py-2 text-amber-900" role="alert">
				{session.Error}
			</p>
		{/if}
		{#if error}
			<p class="mb-3 rounded-lg bg-rose-100 px-4 py-2 text-rose-800" role="alert">{error}</p>
		{/if}

		<div class="sticky top-0 z-10 mb-3 bg-stone-50/95 py-1 backdrop-blur">
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
		</div>

		{#if session.Pages.length === 0}
			<p class="py-12 text-center text-stone-500">
				{scanning ? 'Waiting for the first page…' : 'No pages.'}
			</p>
		{/if}
		<PageGrid
			sessionId={id}
			pages={session.Pages}
			{selection}
			ontoggle={(i) => (selection = toggle(selection, i))}
			onopen={open}
			reorderable={editable}
			onreorder={(order) => {
				selection = clear();
				send(reorderEdit(order));
			}}
		/>
		{#if scanning}
			<p class="mt-4 text-center text-stone-600" aria-live="polite">
				Scanning… {session.Pages.length} page{session.Pages.length === 1 ? '' : 's'} so far
			</p>
		{/if}

		{#if previewIndex !== null}
			<Preview
				sessionId={id}
				pages={session.Pages}
				index={previewIndex}
				onnavigate={(i) => (previewIndex = i)}
				onclose={() => (previewIndex = null)}
				onrotate={(i, deg) => send(rotateEdit(deg, new Set([i])))}
			/>
		{/if}

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
	{/if}
</div>
