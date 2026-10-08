<script lang="ts">
	import type { Preset } from '$lib/presets';
	import { Icon } from '$lib/icons';

	let {
		presets,
		active,
		edited = null,
		modified,
		onpick,
		onsave
	}: {
		presets: Preset[];
		/** Id of the matching preset, or null when nothing matches. */
		active: string | null;
		/** Id of the preset picked last, when a setting has since been changed away from it. */
		edited?: string | null;
		/** Settings differ from every preset. */
		modified: boolean;
		onpick: (id: string) => void;
		onsave: () => void;
	} = $props();
</script>

<div class="flex flex-col items-center gap-2">
	<span class="text-[11px] font-bold tracking-[0.08em] text-ink-faint uppercase">Preset</span>
	<div
		class="flex flex-wrap justify-center gap-1.5 rounded-[24px] bg-[rgb(238_242_255_/_0.8)] p-[5px]"
	>
		{#each presets as preset (preset.id)}
			<button
				type="button"
				class="relative flex h-[38px] items-center gap-2 rounded-full px-3.5 text-[13px] font-bold transition-[background-color,color,box-shadow,transform] duration-200 ease-spring hover:-translate-y-px active:scale-[0.97] {active ===
				preset.id
					? 'bg-accent text-white shadow-preset'
					: edited === preset.id
						? 'bg-surface text-accent-deep shadow-ring-outline'
						: 'bg-surface text-accent-deep shadow-chip'}"
				aria-pressed={active === preset.id}
				title={edited === preset.id ? 'Edited — press to go back to it' : undefined}
				onclick={() => onpick(preset.id)}
			>
				{#if edited === preset.id}
					<span
						class="absolute top-0.5 right-1 h-2 w-2 rounded-full bg-warning ring-2 ring-surface"
						aria-hidden="true"
					></span>
				{/if}
				<Icon name={preset.icon} />
				{preset.name}
			</button>
		{/each}

		{#if modified}
			<span
				class="flex h-[38px] animate-pop-in items-center gap-1.5 rounded-full bg-warning-bg px-3.5 text-[13px] font-bold text-warning-ink"
			>
				Custom
				<button type="button" class="underline underline-offset-2" onclick={onsave}>Save…</button>
			</span>
		{/if}
	</div>
</div>
