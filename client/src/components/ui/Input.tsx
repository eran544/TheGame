import React from 'react';
import styles from './Input.module.css';

export interface InputProps
  extends React.InputHTMLAttributes<HTMLInputElement> {
  label?: string;
  error?: string;
  helperText?: string;
}

const Input: React.FC<InputProps> = ({
  label,
  error,
  helperText,
  id,
  className,
  ...rest
}) => {
  const inputId = id ?? (label ? label.toLowerCase().replace(/\s+/g, '-') : undefined);

  const inputClasses = [
    styles.input,
    error ? styles.inputError : '',
    className ?? '',
  ]
    .filter(Boolean)
    .join(' ');

  return (
    <div className={styles.wrapper}>
      {label && (
        <label htmlFor={inputId} className={styles.label}>
          {label}
        </label>
      )}
      <input id={inputId} className={inputClasses} {...rest} />
      {error && <p className={styles.errorText}>{error}</p>}
      {!error && helperText && (
        <p className={styles.helperText}>{helperText}</p>
      )}
    </div>
  );
};

export default Input;
