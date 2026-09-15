import { createFileRoute, Link, useNavigate } from '@tanstack/react-router';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { Button } from '@vsite/ui';
import { postAuthLoginBody, usePostAuthLogin, type LoginRequest } from '@vsite/api-sdk';
import { getErrorCode } from '@vsite/shared';
import { FormField } from '../components/form-field';
import { getErrorMessage } from '../lib/error-messages';
import { useSessionStore } from '../stores/session-store';

export const Route = createFileRoute('/login')({
  component: LoginPage,
});

function LoginPage() {
  const navigate = useNavigate();
  const setSession = useSessionStore((state) => state.setSession);
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<LoginRequest>({
    resolver: zodResolver(postAuthLoginBody),
    defaultValues: { email: '', password: '' },
  });

  const loginMutation = usePostAuthLogin({
    mutation: {
      onSuccess: (result) => {
        setSession({ accessToken: result.accessToken, refreshToken: result.refreshToken });
        void navigate({ to: '/shops' });
      },
      onError: (error) => {
        setError('root', { message: getErrorMessage(getErrorCode(error)) });
      },
    },
  });

  const submitForm = handleSubmit((values) => {
    loginMutation.mutate({ data: values });
  });

  return (
    <main className="mx-auto mt-16 max-w-sm p-6">
      <h1 className="text-xl font-semibold text-foreground">Đăng nhập</h1>
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
          {isSubmitting ? 'Đang đăng nhập...' : 'Đăng nhập'}
        </Button>
      </form>
      <p className="mt-4 text-sm text-muted-foreground">
        Chưa có tài khoản?{' '}
        <Link to="/register" className="text-primary underline">
          Đăng ký
        </Link>
      </p>
    </main>
  );
}
