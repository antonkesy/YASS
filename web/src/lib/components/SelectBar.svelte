<script lang="ts">
	import { Icon } from '$lib/icons';
	import { t } from '$lib/i18n/index.svelte';

	let {
		count,
		total,
		busy = false,
		onselectall,
		onselectnone,
		ondownload,
		ondelete,
		ondone
	}: {
		count: number;
		total: number;
		busy?: boolean;
		onselectall: () => void;
		onselectnone: () => void;
		ondownload: () => void;
		ondelete: () => void;
		ondone: () => void;
	} = $props();

	let confirmDelete = $state(false);

	const all = $derived(count === total && total > 0);
	const none = $derived(count === 0);

	// Deleting several scans at once is worth a second press; the bar says how many.
	function remove() {
		if (!confirmDelete) {
			confirmDelete = true;
			return;
		}
		confirmDelete = false;
		ondelete();
	}

	$effect(() => {
		if (none) confirmDelete = false;
	});

	const iconButton =
		'flex h-[34px] w-[34px] items-center justify-center rounded-full transition-colors hover:bg-white/20 disabled:pointer-events-none disabled:opacity-45';
</script>

<div
	class="flex animate-pop-in flex-wrap items-center gap-2 rounded-[14px] bg-accent py-1 pr-1 pl-3.5 text-white"
	data-testid="select-bar"
>
	<span class="text-sm font-extrabold tabular-nums">
		{count > 0 ? t('home.selectedCount', { n: count }) : t('home.selectScans')}
	</span>

	<button
		type="button"
		class="h-[26px] rounded-full bg-white/15 px-2.5 text-xs font-bold transition-colors hover:bg-white/25"
		onclick={all ? onselectnone : onselectall}
	>
		{all ? t('home.selectNone') : t('home.selectAll')}
	</button>

	<div class="flex-1"></div>

	<button
		type="button"
		class={iconButton}
		disabled={none || busy}
		onclick={ondownload}
		aria-label={t('home.downloadSelected')}
		data-testid="bulk-download"
	>
		<Icon name="download" />
	</button>

	{#if confirmDelete}
		<button
			type="button"
			class="h-[34px] rounded-full bg-white px-3 text-xs font-extrabold text-danger transition-colors hover:bg-danger-bg"
			onclick={remove}
			onblur={() => (confirmDelete = false)}
			data-testid="bulk-delete"
		>
			{t('home.confirmDeleteCount', { n: count })}
		</button>
	{:else}
		<button
			type="button"
			class={iconButton}
			disabled={none || busy}
			onclick={remove}
			aria-label={t('home.deleteSelected')}
			data-testid="bulk-delete"
		>
			<Icon name="trash" />
		</button>
	{/if}

	<button
		type="button"
		class="h-[30px] rounded-full bg-white px-3 text-[13px] font-extrabold text-accent-deep transition-colors hover:bg-accent-50"
		onclick={ondone}
		data-testid="select-done"
	>
		{t('home.done')}
	</button>
</div>
