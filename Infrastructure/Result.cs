namespace GitBuddy.Infrastructure
{
    /// <summary>
    /// Represents the result of an operation that can succeed or fail.
    /// </summary>
    public record Result<T>
    {
        public bool IsSuccess { get; init; }
        public bool IsFailure => !IsSuccess;
        public T? Value { get; init; }
        public string Error { get; init; } = string.Empty;

        private Result(bool isSuccess, T? value, string error)
        {
            IsSuccess = isSuccess;
            Value = value;
            Error = error;
        }

        /// <summary>
        /// Creates a successful result with a value.
        /// </summary>
        public static Result<T> Success(T value) => new(true, value, string.Empty);

        /// <summary>
        /// Creates a failed result with an error message.
        /// </summary>
        public static Result<T> Failure(string error) => new(false, default, error);

        /// <summary>
        /// Executes an action if the result is successful.
        /// </summary>
        public Result<T> OnSuccess(Action<T> action)
        {
            if (IsSuccess && Value != null)
            {
                action(Value);
            }
            return this;
        }

        /// <summary>
        /// Executes an action if the result is a failure.
        /// </summary>
        public Result<T> OnFailure(Action<string> action)
        {
            if (IsFailure)
            {
                action(Error);
            }
            return this;
        }

        /// <summary>
        /// Maps the value to a new type if successful.
        /// </summary>
        public Result<TNew> Map<TNew>(Func<T, TNew> mapper)
        {
            return IsSuccess && Value != null
                ? Result<TNew>.Success(mapper(Value))
                : Result<TNew>.Failure(Error);
        }
    }

    /// <summary>
    /// Represents the result of an operation without a return value.
    /// </summary>
    public record Result
    {
        public bool IsSuccess { get; init; }
        public bool IsFailure => !IsSuccess;
        public string Error { get; init; } = string.Empty;

        private Result(bool isSuccess, string error)
        {
            IsSuccess = isSuccess;
            Error = error;
        }

        /// <summary>
        /// Creates a successful result.
        /// </summary>
        public static Result Success() => new(true, string.Empty);

        /// <summary>
        /// Creates a failed result with an error message.
        /// </summary>
        public static Result Failure(string error) => new(false, error);

        /// <summary>
        /// Executes an action if the result is successful.
        /// </summary>
        public Result OnSuccess(Action action)
        {
            if (IsSuccess)
            {
                action();
            }
            return this;
        }

        /// <summary>
        /// Executes an action if the result is a failure.
        /// </summary>
        public Result OnFailure(Action<string> action)
        {
            if (IsFailure)
            {
                action(Error);
            }
            return this;
        }
    }
}
