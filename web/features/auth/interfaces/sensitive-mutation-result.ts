export interface SensitiveMutationResult<TInput> {
  submit: (input: TInput) => Promise<void>;
  isPending: boolean;
  isSuccess: boolean;
  error: Error | null;
  reset: () => void;
}
