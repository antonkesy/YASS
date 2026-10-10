<script lang="ts">
	import { bounceWhenPicked } from '$lib/motion';
	import { t } from '$lib/i18n/index.svelte';
	import type { Preset } from '$lib/presets';
	import { Icon } from '$lib/icons';
	import PresetPill from './PresetPill.svelte';

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
		/** Settings differ from every preset, so they can be saved as one. */
		modified: boolean;
		onpick: (id: string) => void;
		onsave: () => void;
	} = $props();

	// The pill that lights up bounces, but not on load: nothing was picked then.
	const bounce = bounceWhenPicked(() => active);
</script>

<div class="flex flex-col items-center gap-2">
	<!--
		The heading line carries the Save as preset link, beside the label rather than in the row
		of presets: there it came and went with a pill of its own, rewrapping the row and moving
		everything under it. This line is a fixed height with or without it, and the label stays
		centred on its own column.
	-->
	<div class="grid h-8 w-full grid-cols-[1fr_auto_1fr] items-center gap-2">
		<span></span>
		<span class="text-[13px] font-semibold text-ink-muted">{t('home.preset')}</span>
		{#if modified}
			<button
				type="button"
				class="flex h-8 animate-fade-in items-center gap-1 justify-self-start rounded-full px-2 text-[12px] font-bold whitespace-nowrap text-accent transition-colors hover:bg-accent-wash"
				onclick={onsave}
				title={t('home.saveAsPresetHint')}
				data-testid="preset-save-current"
			>
				<Icon name="plus" />
				{t('home.saveAsPreset')}
			</button>
		{/if}
	</div>
	<div class="flex flex-wrap justify-center gap-1.5 rounded-[24px] bg-ctl-track p-[4px]">
		{#each presets as preset (preset.id)}
			<PresetPill
				{preset}
				active={active === preset.id}
				edited={edited === preset.id}
				editedTitle={t('home.presetEdited')}
				tray
				onclick={() => onpick(preset.id)}
				{@attach bounce(preset.id)}
			/>
		{/each}
	</div>
</div>
