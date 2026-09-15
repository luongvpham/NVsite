import { createFileRoute, Link } from '@tanstack/react-router';
import { zodResolver } from '@hookform/resolvers/zod';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { Button } from '@vsite/ui';
import { postAuthRegisterBody, usePostAuthRegister, type RegisterRequest } from '@vsite/api-sdk';
import { getErrorCode } from '@vsite/shared';
import { FormField } from '../components/form-field';
import { getErrorMessage } from '../lib/error-messages';

export const Route = createFileRoute('/register')({
  component: RegisterPage,
});

function RegisterPage() {
  const [submitted, setSubmitted] = useState(false);
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<RegisterRequest>({
    resolver: zodResolver(postAuthRegisterBody),
    defaultValues: { email: '', password: '', fullName: '' },
  });

  const registerMutation = usePostAuthRegister({
    mutation: {
      onSuccess: () => {
        setSubmitted(true);
      },
      onError: (error) => {
        setError('root', { message: getErrorMessage(getErrorCode(error)) });
      },
    },
  });

  const submitForm = handleSubmit((values) => {
    registerMutation.mutate({ data: { ...values, fullName: values.fullName || null } });
  });

  if (submitted) {
    return (
      <main className="mx-auto mt-16 max-w-sm p-6 text-center">
        <h1 className="text-xl font-semibold text-foreground">Kiểm tra email để xác minh</h1>
        <p className="mt-2 text-muted-foreground">
          Chúng tôi đã gửi link xác minh tới email của bạn. Sau khi xác minh xong, hãy{' '}
          <Link to="/login" className="text-primary underline">
            đăng nhập
          </Link>
          .
        </p>
      </main>
    );
  }

  return (
    <main className="mx-auto mt-16 max-w-sm p-6">
      <h1 className="text-xl font-semibold text-foreground">Đăng ký</h1>
      <form
        className="mt-6 space-y-4"
        onSubmit={(event) => {
          void submitForm(event);
        }}
        noValidate
      >
        <FormField
          id="email"
          label="Email"
          type="email"
          required
          error={errors.email?.message}
          {...register('email')}
        />
        <FormField id="fullName" label="Họ tên" type="text" {...register('fullName')} />
        <FormField
          id="password"
          label="Mật khẩu"
          type="password"
          required
          error={errors.password?.message}
          {...register('password')}
        />
        {errors.root && (
          <p className="text-sm text-destructive" role="alert">
            {errors.root.message}
          </p>
        )}
        <Button type="submit" disabled={isSubmitting} className="w-full">
          {isSubmitting ? 'Đang gửi...' : 'Đăng ký'}
        </Button>
      </form>
      <p className="mt-4 text-sm text-muted-foreground">
        Đã có tài khoản?{' '}
        <Link to="/login" className="text-primary underline">
          Đăng nhập
        </Link>
      </p>
    </main>
  );
}
