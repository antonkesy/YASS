/**
 * Models small enough to decode on a processor, for the CPU engine (ADR-011).
 *
 * ADR-010 could say the catalogue comes from the package and so cannot go stale here. wllama
 * ships none, so this list is ours — which is why every URL names a **commit sha** rather than
 * a branch. The bytes behind a sha cannot change, so the size beside it cannot start lying.
 *
 * Plain data on purpose: the settings page imports this directly to fill its dropdown, without
 * loading the worker or the eight megabytes of WebAssembly behind it.
 */

export type CpuModel = {
	/** What the dropdown shows, and what the suggestions popover credits the name to. */
	label: string;
	url: string;
	bytes: number;
};

const hf = (repo: string, sha: string, file: string) =>
	`https://huggingface.co/${repo}/resolve/${sha}/${file}`;

/**
 * Smallest first, like the GPU engine sorts its own. All instruct-tuned and none of them
 * reasoning models: thinking tokens would eat the 64-token budget in `prompt.ts` before a
 * file name came out.
 */
export const CPU_MODELS: readonly CpuModel[] = [
	{
		label: 'SmolLM2 135M Instruct (Q8_0)',
		url: hf(
			'bartowski/SmolLM2-135M-Instruct-GGUF',
			'09816acd5d99df7be770d85ea30822623dab342c',
			'SmolLM2-135M-Instruct-Q8_0.gguf'
		),
		bytes: 144_811_360
	},
	{
		label: 'SmolLM2 360M Instruct (Q4_K_M)',
		url: hf(
			'bartowski/SmolLM2-360M-Instruct-GGUF',
			'7be6f65f1db715fe5dc5a4634c0d459b4eed42ec',
			'SmolLM2-360M-Instruct-Q4_K_M.gguf'
		),
		bytes: 270_590_880
	},
	{
		label: 'Qwen2.5 0.5B Instruct (Q4_K_M)',
		url: hf(
			'bartowski/Qwen2.5-0.5B-Instruct-GGUF',
			'41ba88dbac95fed2528c92514c131d73eb5a174b',
			'Qwen2.5-0.5B-Instruct-Q4_K_M.gguf'
		),
		bytes: 397_808_192
	},
	{
		label: 'Llama 3.2 1B Instruct (Q4_K_M)',
		url: hf(
			'bartowski/Llama-3.2-1B-Instruct-GGUF',
			'067b946cf014b7c697f3654f621d577a3e3afd1c',
			'Llama-3.2-1B-Instruct-Q4_K_M.gguf'
		),
		bytes: 807_694_464
	}
];

/** Megabytes, to sit beside a label the way the GPU engine's sizes do. */
export function cpuModelSizeMb(bytes: number): number {
	return Math.round(bytes / 1_000_000);
}

/**
 * What to call a model on screen. The setting holds a URL, because a pasted one and a chosen
 * one have to be the same field — but "Written in this browser by https://huggingface.co/…"
 * is not a sentence, so a URL falls back to its file name.
 */
export function cpuModelLabel(url: string): string {
	const known = CPU_MODELS.find((m) => m.url === url);
	if (known) return known.label;

	const file = url.split('?')[0].split('/').filter(Boolean).pop();
	return file ? file.replace(/\.gguf$/i, '') : url;
}
