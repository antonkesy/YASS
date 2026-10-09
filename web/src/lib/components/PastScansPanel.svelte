<script lang="ts">
	import type { SessionDto } from '$lib/hub/generated/Yass.Core.Hub';
	import { browse, statusCounts, type SortOrder, type StatusFilter } from '$lib/sessions/browse';
	import { formatBytes } from '$lib/format';
	import { t } from '$lib/i18n/index.svelte';
	import { exportPrefs } from '$lib/presets';
	import SessionRow from './SessionRow.svelte';
	import SessionTile from './SessionTile.svelte';
	import SortMenu from './SortMenu.svelte';
	import StatusFilterChips from './StatusFilter.svelte';
	import EmptyState from './EmptyState.svelte';
	import SelectBar from './SelectBar.svelte';
	import { Icon } from '$lib/icons';
	import { clear, retain, toggle } from '$lib/review/selection';

	let {
		sessions,
		landed = null,
		busy = false,
		onclear,
		ondownload,
		ondelete
	}: {
		sessions: SessionDto[];
		/** Id of the scan to play the landing highlight on, once. */
		landed?: string | null;
		busy?: boolean;
		onclear?: () => void;
		ondownload?: (ids: string[]) => void;
		ondelete?: (ids: string[]) => void;
	} = $props();

	let view = $state<'grid' | 'list'>('grid');
	let query = $state('');
	let order = $state<SortOrder>('newest');
	let status = $state<StatusFilter>('all');
	let confirmClear = $state(false);
	let selecting = $state(false);
	let picked = $state<Set<string>>(clear());

	// What an unnamed scan is called, so the list, the search box and the A-Z sort all read
	// the same name the review screen shows and the save writes.
	const template = $derived($exportPrefs.nameTemplate);

	const counts = $derived(statusCounts(sessions));
	const shown = $derived(browse(sessions, { query, status, order, template }));
	const bytes = $derived(sessions.reduce((n, s) => n + s.SizeBytes, 0));

	// A filter that no longer matches anything would otherwise strand the panel empty
	// with no chip to click back to.
	$effect(() => {
		if (status !== 'all' && (counts[status] ?? 0) === 0) status = 'all';
	});

	// Selections follow what is on screen: a scan filtered out of view is no longer
	// something the bulk buttons should silently act on.
	$effect(() => {
		const visible = new Set(shown.map((s) => s.Id));
		const kept = retain(picked, visible);
		if (kept.size !== picked.size) picked = kept;
	});

	function endSelecting() {
		selecting = false;
		picked = clear();
	}

	function clearEverything() {
		if (!confirmClear) {
			confirmClear = true;
			return;
		}
		confirmClear = false;
		onclear?.();
	}
</script>

<section
	class="flex min-h-0 animate-fade-in flex-col gap-4 rounded-[28px] bg-surface p-[22px] shadow-card"
	style:flex="1.25 1 420px"
	data-testid="past-scans"
>
	<div class="flex items-center gap-2">
		<!-- No count up here: the foot of the panel says how many, and the empty state says none. -->
		<h2 class="text-[19px] font-extrabold tracking-[-0.01em]">{t('home.pastScans')}</h2>
		<div class="flex-1"></div>
		<div
			class="flex gap-0 rounded-[12px] bg-ctl-track p-[3px]"
			role="group"
			aria-label={t('home.view')}
		>
			{#each [{ key: 'grid', icon: 'grid', label: t('home.gridView') }, { key: 'list', icon: 'list', label: t('home.listView') }] as const as option (option.key)}
				<button
					type="button"
					class="flex h-[30px] w-[34px] items-center justify-center rounded-[9px] transition-all duration-200 {view ===
					option.key
						? 'bg-surface text-accent shadow-seg'
						: 'text-ink-muted'}"
					aria-pressed={view === option.key}
					aria-label={option.label}
					onclick={() => (view = option.key)}
				>
					<Icon name={option.icon} />
				</button>
			{/each}
		</div>
	</div>

	{#if selecting}
		<SelectBar
			count={picked.size}
			total={shown.length}
			{busy}
			onselectall={() => (picked = new Set(shown.map((s) => s.Id)))}
			onselectnone={() => (picked = clear())}
			ondownload={() => ondownload?.([...picked])}
			ondelete={() => {
				ondelete?.([...picked]);
				endSelecting();
			}}
			ondone={endSelecting}
		/>
	{:else if sessions.length > 0}
		<div class="flex gap-2">
			<!--
				min-w-0 here and w-0 on the input: an input keeps a default width of about twenty
				characters however flexible its box, which with Sort and Select beside it made this
				row wider than a phone.
			-->
			<label class="flex h-[38px] min-w-0 flex-1 items-center gap-2 rounded-[12px] bg-ctl px-3">
				<span class="text-ink-faint"><Icon name="search" /></span>
				<span class="sr-only">{t('home.searchPastScans')}</span>
				<input
					class="w-0 min-w-0 flex-1 bg-transparent text-sm font-medium outline-none"
					type="search"
					placeholder={t('home.searchScans')}
					bind:value={query}
					data-testid="session-search"
				/>
			</label>
			<SortMenu {order} onchange={(o) => (order = o)} />
			<button
				type="button"
				class="h-[38px] rounded-[12px] bg-accent-50 px-3 text-[13px] font-bold text-accent-deep transition-colors hover:bg-accent-100"
				onclick={() => (selecting = true)}
				data-testid="select-button"
			>
				{t('home.select')}
			</button>
		</div>
	{/if}

	{#if !selecting && sessions.length > 0}
		<StatusFilterChips
			value={status}
			{counts}
			total={sessions.length}
			onchange={(s) => (status = s)}
		/>
	{/if}

	<!-- The list is the only part that scrolls, so the search row and totals stay put. -->
	<div class="-mx-1 flex min-h-0 flex-1 flex-col overflow-y-auto px-1">
		{#if shown.length === 0}
			<EmptyState
				title={sessions.length === 0
					? t('home.emptyTitle')
					: query.trim()
						? t('home.noMatches')
						: t('home.noStatusMatches')}
				body={sessions.length === 0
					? t('home.emptyBody')
					: query.trim()
						? t('home.noMatchesBody', { query: query.trim() })
						: t('home.noStatusMatchesBody')}
			/>
		{:else if view === 'grid'}
			<div
				class="grid gap-3"
				style="grid-template-columns: repeat(auto-fill, minmax(118px, 1fr))"
				data-testid="session-list"
			>
				{#each shown as session, i (session.Id)}
					<div class="animate-page-in" style="animation-delay: {Math.min(i, 10) * 45}ms">
						<SessionTile
							{session}
							{template}
							landed={session.Id === landed}
							selectable={selecting}
							selected={picked.has(session.Id)}
							ontoggle={(id) => (picked = toggle(picked, id))}
						/>
					</div>
				{/each}
			</div>
		{:else}
			<div class="flex flex-col gap-1" data-testid="session-list">
				{#each shown as session, i (session.Id)}
					<div class="animate-page-in" style="animation-delay: {Math.min(i, 10) * 45}ms">
						<SessionRow
							{session}
							{template}
							landed={session.Id === landed}
							selectable={selecting}
							selected={picked.has(session.Id)}
							ontoggle={(id) => (picked = toggle(picked, id))}
							ondownload={ondownload ? (id) => ondownload([id]) : undefined}
							ondelete={ondelete ? (id) => ondelete([id]) : undefined}
						/>
					</div>
				{/each}
			</div>
		{/if}
	</div>

	{#if sessions.length > 0}
		<div class="mt-auto flex items-center gap-2 border-t border-line pt-3">
			<p class="text-xs text-ink-muted">
				{t('home.scanCount', { n: sessions.length })} · {formatBytes(bytes)}
			</p>
			<div class="flex-1"></div>
			{#if onclear}
				<button
					type="button"
					class="h-8 rounded-full px-3 text-xs font-bold transition-colors {confirmClear
						? 'bg-danger text-white'
						: 'text-danger hover:bg-danger-bg'}"
					onclick={clearEverything}
					onblur={() => (confirmClear = false)}
					data-testid="clear-all"
				>
					{confirmClear ? t('home.confirmClearAll') : t('home.clearAll')}
				</button>
			{/if}
		</div>
	{/if}
</section>
