<script lang="ts">
	import { Icon } from '$lib/icons';

	let {
		value,
		tokens,
		preview,
		unknown,
		onchange
	}: {
		value: string;
		/** Tokens offered as chips, without braces. */
		tokens: readonly string[];
		/** What the template expands to right now. */
		preview: string;
		/** Tokens in the template the server does not know. */
		unknown: readonly string[];
		onchange: (value: string) => void;
	} = $props();

	let field = $state<HTMLInputElement | null>(null);
	/** Where the caret was when the field last lost focus, so a chip inserts in place. */
	let caret = $state<[number, number]>([0, 0]);

	function remember() {
		if (field) caret = [field.selectionStart ?? 0, field.selectionEnd ?? 0];
	}

	function insert(token: string) {
		const text = `{${token}}`;
		const [from, to] =
			field && document.activeElement === field
				? [field.selectionStart ?? value.length, field.selectionEnd ?? value.length]
				: caret;
		const next = value.slice(0, from) + text + value.slice(to);
		onchange(next);
		const at = from + text.length;
		caret = [at, at];
		requestAnimationFrame(() => {
			field?.focus();
			field?.setSelectionRange(at, at);
		});
	}
</script>

<div class="flex flex-col gap-2">
	<label class="flex h-11 items-center gap-2 rounded-[12px] bg-ctl px-3">
		<span class="text-ink-faint"><Icon name="template" /></span>
		<span class="sr-only">File name template</span>
		<input
			bind:this={field}
			class="min-w-0 flex-1 bg-transparent font-mono text-sm font-medium outline-none"
			type="text"
			placeholder="{'{YEAR}'}-{'{MONTH}'}-{'{DAY}'}_scan"
			{value}
			oninput={(e) => onchange(e.currentTarget.value)}
			onblur={remember}
			onkeyup={remember}
			onclick={remember}
			data-testid="template-input"
		/>
		<span class="font-mono text-[13px] font-semibold text-ink-faint">.pdf</span>
	</label>

	<div class="flex flex-wrap gap-1.5">
		{#each tokens as token (token)}
			<button
				type="button"
				class="flex h-7 items-center rounded-[8px] bg-accent-50 px-[9px] font-mono text-xs font-semibold text-accent-deep transition-[background-color,transform] duration-150 hover:-translate-y-px hover:bg-accent-100 active:scale-95"
				onclick={() => insert(token)}
			>
				{'{' + token + '}'}
			</button>
		{/each}
	</div>

	{#if unknown.length > 0}
		<p class="text-xs font-semibold text-danger" role="alert" data-testid="template-error">
			Unknown: {unknown.map((t) => `{${t}}`).join(', ')}
		</p>
	{:else if preview}
		<p
			class="rounded-[10px] bg-success-bg px-3 py-1.5 font-mono text-[13px] font-semibold text-success-ink"
			data-testid="template-preview"
		>
			{preview}
		</p>
	{/if}

	<p class="text-[11px] text-ink-faint">
		Click a tag to add it at the cursor. Scans you have not named are saved with this.
	</p>
</div>
