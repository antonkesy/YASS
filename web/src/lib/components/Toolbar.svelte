<script lang="ts">
	import { ColorMode } from '$lib/hub/generated/Yass.Core.Domain';
	import { colorModeLabels } from '$lib/review/edits';
	import { Icon } from '$lib/icons';

	let {
		selectedCount,
		total,
		anyDeleted,
		disabled = false,
		onrotate,
		ondelete,
		onrestore,
		oncolor,
		onclear
	}: {
		selectedCount: number;
		total: number;
		anyDeleted: boolean;
		disabled?: boolean;
		onrotate: (degrees: 90 | -90) => void;
		ondelete: () => void;
		onrestore: () => void;
		oncolor: (mode: ColorMode | undefined) => void;
		onclear: () => void;
	} = $props();

	const scope = $derived(selectedCount === 0 ? `all ${total}` : `${selectedCount} selected`);

	// Icon over label, the mockup's tool shape. Crop, rescan and split join this row
	// once the backend can do them.
	const tool =
		'flex h-[58px] min-w-[62px] flex-col items-center justify-center gap-1 rounded-[14px] px-2 text-[11px] font-bold transition-colors hover:bg-accent-hover50 disabled:pointer-events-none disabled:opacity-35';
</script>

<div
	class="flex flex-wrap items-center gap-0.5 rounded-[20px] bg-surface p-1.5 shadow-toolbar"
	role="toolbar"
	aria-label="Page edits"
	data-testid="toolbar"
>
	<span class="px-3 text-[13px] font-semibold text-ink-muted" data-testid="toolbar-scope">
		Acting on {scope}
	</span>

	{#if selectedCount > 0}
		<button
			type="button"
			class="h-9 rounded-full px-3 text-[13px] font-bold text-accent transition-colors hover:bg-accent-wash"
			onclick={onclear}
		>
			Clear
		</button>
	{/if}

	<span class="mx-1 h-8 w-px bg-line" aria-hidden="true"></span>

	<button
		type="button"
		class="text-ink {tool}"
		{disabled}
		onclick={() => onrotate(-90)}
		aria-label="Rotate left"
	>
		<Icon name="rotateLeft" />
		Left
	</button>
	<button
		type="button"
		class="text-ink {tool}"
		{disabled}
		onclick={() => onrotate(90)}
		aria-label="Rotate right"
	>
		<Icon name="rotateRight" />
		Right
	</button>

	<label class="flex h-[58px] min-w-[62px] flex-col items-center justify-center gap-1 px-2">
		<span class="sr-only">Colour mode</span>
		<Icon name="wand" />
		<select
			class="w-full bg-transparent text-center text-[11px] font-bold text-ink-muted"
			{disabled}
			data-testid="color-select"
			onchange={(e) => {
				const v = (e.currentTarget as HTMLSelectElement).value;
				oncolor(v === '' ? undefined : (Number(v) as ColorMode));
				(e.currentTarget as HTMLSelectElement).value = '';
			}}
		>
			<option value="">Filter</option>
			<option value="">Scan default</option>
			{#each Object.values(ColorMode).filter((v) => typeof v === 'number') as mode (mode)}
				<option value={mode}>{colorModeLabels[mode as ColorMode]}</option>
			{/each}
		</select>
	</label>

	<button
		type="button"
		class="text-ink {tool}"
		disabled={disabled || !anyDeleted}
		onclick={onrestore}
		data-testid="restore-button"
	>
		<Icon name="rescan" />
		Restore
	</button>
	<button
		type="button"
		class="text-danger hover:bg-danger-bg! {tool}"
		{disabled}
		onclick={ondelete}
		data-testid="delete-button"
	>
		<Icon name="trash" />
		Delete
	</button>
</div>
